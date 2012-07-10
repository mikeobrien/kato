using System;
using System.Net.Sockets;
using System.Text;

namespace Kato
{
    /// <summary>
	/// Maintains the current state for a SMTP client connection.
	/// </summary>
	/// <remarks>
	/// This class is similar to a HTTP Session.  It is used to maintain all
	/// the state information about the current connection.
	/// </remarks>
	public class SmtpContext
	{
		private const string Eol = "\r\n";
		
		/// <summary>The unique ID assigned to this connection</summary>
		private readonly long _connectionId;
		
		/// <summary>The socket to the client.</summary>
		private readonly Socket _socket;

        private readonly ILog _logger;

        /// <summary>Last successful command received.</summary>
		private int _lastCommand;

        /// <summary>The incoming message.</summary>
		private SmtpMessageData _messageData;
		
		/// <summary>Encoding to use to send/receive data from the socket.</summary>
		private readonly Encoding _encoding;
		
		/// <summary>
		/// It is possible that more than one line will be in
		/// the queue at any one time, so we need to store any input
		/// that has been read from the socket but not requested by the
		/// ReadLine command yet.
		/// </summary>
		private StringBuilder _inputBuffer;
	
		/// <summary>
		/// Initialize this context for a given socket connection.
		/// </summary>
		public SmtpContext(long connectionId, Socket socket, ILog logger)
		{
		    _logger = logger ?? new NullLogger();
            _logger.Debug("Connection {0}: New connection from client {1}", connectionId, socket.RemoteEndPoint);
			
			_connectionId = connectionId;
			_lastCommand = -1;
			_socket = socket;
		    _messageData = new SmtpMessageData();
			
			// Set the encoding to ASCII.  
			_encoding = Encoding.ASCII;
			
			// Initialize the input buffer
			_inputBuffer = new StringBuilder();
		}
		
		/// <summary>
		/// The unique connection id.
		/// </summary>
		public long ConnectionId
		{
			get
			{
				return _connectionId;
			}
		}
		
		/// <summary>
		/// Last successful command received.
		/// </summary>
		public int LastCommand
		{
			get
			{
				return _lastCommand;
			}
			set
			{
				_lastCommand = value;
			}
		}

        /// <summary>
        /// The client domain, as specified by the helo command.
        /// </summary>
        public string ClientDomain { get; set; }

        /// <summary>
		/// The Socket that is connected to the client.
		/// </summary>
		public Socket Socket
		{
			get
			{
				return _socket;
			}
		}
		
		/// <summary>
		/// The SMTPMessage that is currently being received.
		/// </summary>
		public SmtpMessageData MessageData
		{
			get
			{
				return _messageData;
			}
			set
			{
				_messageData = value;
			}
		}
		
		/// <summary>
		/// Writes the string to the socket as an entire line.  This
		/// method will append the end of line characters, so the data
		/// parameter should not contain them.
		/// </summary>
		/// <param name="data">The data to write the the client.</param>
		public void WriteLine(string data)
		{
            _logger.Debug("Connection {0}: Wrote Line: {1}", _connectionId, data);
			_socket.Send(_encoding.GetBytes(data + Eol));
		}
		
		/// <summary>
		/// Reads an entire line from the socket.  This method
		/// will block until an entire line has been read.
		/// </summary>
		public string ReadLine()
		{
			// If we already buffered another line, just return
			// from the buffer.			
			var output = ReadBuffer();
			if (output != null)
			{
				return output;
			}
						
			// Otherwise, read more input.
			var byteBuffer = new byte[80];

		    // Read from the socket until an entire line has been read.			
			do
			{
				// Read the input data.
				var count = _socket.Receive(byteBuffer);
				
				if (count == 0)
				{
                    _logger.Debug("Socket closed before end of line received.");
					return null;
				}

				_inputBuffer.Append(_encoding.GetString(byteBuffer, 0, count));
                _logger.Debug("Connection {0}: Read: {1}", _connectionId, _inputBuffer);
			}
			while((output = ReadBuffer()) == null);
			
			// IO Log statement is in ReadBuffer...
			
			return output;
		}
		
		/// <summary>
		/// Resets this context for a new message
		/// </summary>
		public void Reset()
		{
            _logger.Debug("Connection {0}: Reset", _connectionId);
			_messageData = new SmtpMessageData();
			_lastCommand = SmtpHandler.CommandHelo;
		}
		
		/// <summary>
		/// Closes the socket connection to the client and performs any cleanup.
		/// </summary>
		public void Close()
		{
			_socket.Close();
		}
		
		/// <summary>
		/// Helper method that returns the first full line in
		/// the input buffer, or null if there is no line in the buffer.
		/// If a line is found, it will also be removed from the buffer.
		/// </summary>
		private string ReadBuffer()
		{
			// If the buffer has data, check for a full line.
			if (_inputBuffer.Length > 0)				
			{
				var buffer = _inputBuffer.ToString();
				var eolIndex = buffer.IndexOf(Eol);
				if (eolIndex != -1)
				{
					var output = buffer.Substring(0, eolIndex);
					_inputBuffer = new StringBuilder(buffer.Substring(eolIndex + 2));
                    _logger.Debug("Connection {0}: Read Line: {1}", _connectionId, output);
					return output;
				}				
			}
			return null;
		}
	}
}
