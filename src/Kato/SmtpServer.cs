using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Threading;

namespace Kato
{
	/// <summary>
	/// This class provides a bare bones implementation
	/// of a Server to allow the SMTPProcessor or POP3Processor
	/// to handle incoming socket connections.
	/// </summary>
	/// <remarks>
	/// This class provides a very simple server implementation that accepts
	/// incoming Socket connections and passes the call to SMTPProcessor or
	/// POP3Processor for processing.  This code is for example/test use only
	/// and should not be considered a production solution.  
	/// </remarks>
	public class SmtpServer
	{
		private bool _running;
		private TcpListener _listener;
	    private ConcurrentDictionary<Socket, object> _connections;  
		
		private readonly int _port;
	    private readonly ISmtpHandler _handler;

	    /// <summary>
		/// Creates a new SimpleServer that listens on a specific
		/// port for connections and passes them to the specified delagat
		/// </summary>
        /// <param name="domain">
        /// The domain name this server handles mail for.  This does not have to
        /// be a valid domain name, but it will be included in the Welcome Message
        /// and HELO response.
        /// </param>
		/// <param name="port">The port to listen on.</param>
        /// <param name="recipientFilter">
        /// The IRecipientFilter implementation is responsible for 
        /// filtering the recipient addresses to determine which ones
        /// to accept for delivery.
        /// </param>
        /// <param name="messageSpool">
        /// The IMessageSpool implementation is responsible for 
        /// spooling the inbound message once it has been recieved from the sender.
        /// </param>
        /// <param name="logger"> </param>
        public SmtpServer(
            Action<MailMessage> handler,
            string domain = null, 
            int port = 25,
            Func<SmtpContext, MailAddress, bool> recipientFilter = null, 
            ILog logger = null)
		{
			_port = port;
	        domain = domain ?? Environment.MachineName;
            _handler = new SmtpHandler(
                domain,
                handler,
                recipientFilter ?? ((context, address) => 
                    domain == null || domain.Equals(address.Host)),
                logger ?? new NullLogger());
        }

        /// <summary>
        /// Listens for new connections and starts a new thread to handle each
        /// new connection.  Loops infinitely.
        /// </summary>
        public void Start(bool async = true)
		{
            _connections = new ConcurrentDictionary<Socket, object>();
			var endPoint = new IPEndPoint(IPAddress.Any, _port);
			_listener = new TcpListener(endPoint);
			_listener.Start();

			_running = true;

            if (async) ThreadPool.QueueUserWorkItem(x => AcceptConnections());
            else AcceptConnections();
		}

        private void AcceptConnections()
        {
			while(_running)
			{
                try
                {
				    var socket = _listener.AcceptSocket();
                    ThreadPool.QueueUserWorkItem(x => {
                            _connections.TryAdd(socket, null);
                            _handler.HandleConnection(socket);
                            object result;
                            if (_connections.TryRemove(socket, out result)) socket.Close();
                        });	
                } 
                catch(SocketException e)
                {
                    if (e.ErrorCode == 10004) return;
                    throw;
                }	
			}            
        }

		/// <summary>
		/// Stop the server.  This notifies the listener to stop accepting new connections
		/// and that the loop should exit.
		/// </summary>
		public void Stop()
		{
            if (!_running) return;
			_running = false;
			if (_listener != null) _listener.Stop();
            _connections.ToList().ForEach(x => x.Key.Close());
            _connections.Clear();
		}
	}
}
