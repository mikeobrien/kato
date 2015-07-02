using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Kato
{
    /// <summary>
	/// SMTPProcessor handles a single SMTP client connection.  This
	/// class provides an implementation of the RFC821 specification.
	/// </summary>
	public class SmtpHandler : ISmtpHandler
	{
		// Command codes
		/// <summary>HELO Command</summary>
		public const int CommandHelo = 0;
		/// <summary>RSET Command</summary>
	    public const int CommandRset = 1;
		/// <summary>NOOP Command</summary>
	    public const int CommandNoop = 2;
		/// <summary>QUIT Command</summary>
	    public const int CommandQuit = 3;
		/// <summary>MAIL FROM Command</summary>
	    public const int CommandMail = 4;
		/// <summary>RCPT TO Command</summary>
	    public const int CommandRcpt = 5;
		/// <summary>DATA Comand</summary>
	    public const int CommandData = 6;

		// Messages
		private const string MessageDefaultWelcome = "220 {0} Welcome to Kato.";
		private const string MessageDefaultHeloResponse = "250 {0}";
		private const string MessageOk = "250 OK";
		private const string MessageStartData = "354 Start mail input; end with <CRLF>.<CRLF>";
		private const string MessageGoodbye = "221 Goodbye.";

		private const string MessageUnknownCommand = "500 Command Unrecognized.";
		private const string MessageInvalidCommandOrder = "503 Command not allowed here.";
		private const string MessageInvalidArgumentCount = "501 Incorrect number of arguments.";
		
		private const string MessageInvalidAddress = "451 Address is invalid.";
		private const string MessageUnknownUser = "550 User does not exist.";
		
		private const string MessageSystemError = "554 Transaction failed.";
		
		/// <summary>
		/// Every connection will be assigned a unique id to 
		/// provide consistent log output and tracking.
		/// </summary>
		private long _connectionId;
		
		/// <summary>Determines which recipients to accept for delivery.</summary>
        private readonly Func<SmtpContext, MailAddress, bool> _recipientFilter;
		
		/// <summary>Incoming Message spool</summary>
		private readonly IMessageSpool _messageSpool;

		/// <summary>Domain name for this server.</summary>
		private readonly string _domain;

		/// <summary>The message to display to the client when they first connect.</summary>
		private string _welcomeMessage;
		
		/// <summary>The response to the HELO command.</summary>
		private string _heloResponse;

        private readonly ILog _logger;

        /// <summary>
        /// Initializes the SMTPProcessor with the appropriate 
        /// interface implementations.  This allows the relay and
        /// delivery behaviour of the SMTPProcessor to be defined
        /// by the specific server.
        /// </summary>
        /// <param name="domain">
        /// The domain name this server handles mail for.  This does not have to
        /// be a valid domain name, but it will be included in the Welcome Message
        /// and HELO response.
        /// </param>
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
        public SmtpHandler(string domain, IMessageSpool messageSpool, Func<SmtpContext, MailAddress, bool> recipientFilter, ILog logger)
		{
            _logger = logger;
			// Initialize the connectionId counter
			_connectionId = 1;
			
			_domain = domain;
			
			// Initialize default messages
			_welcomeMessage = String.Format(MessageDefaultWelcome, domain);
			_heloResponse = String.Format(MessageDefaultHeloResponse, domain);	
						
			_recipientFilter = recipientFilter;
			_messageSpool = messageSpool;
		}
		
		/// <summary>
		/// Returns the welcome message to display to new client connections.
		/// This method can be overridden to allow for user defined welcome messages.
		/// Please refer to RFC 821 for the rules on acceptable welcome messages.
		/// </summary>
		public virtual string WelcomeMessage
		{
			get
			{
				return _welcomeMessage;
			}
			set
			{
				_welcomeMessage = String.Format(value, _domain);
			}
		}
		
		/// <summary>
		/// The response to the HELO command.  This response should
		/// include the local server's domain name.  Please refer to RFC 821
		/// for more details.
		/// </summary>
		public virtual string HeloResponse
		{
			get
			{
				return _heloResponse;
			}
			set
			{
				_heloResponse = String.Format(value, _domain);
			}
		}
		
		/// <summary>
		/// ProcessConnection handles a connected TCP Client
		/// and performs all necessary interaction with this
		/// client to comply with RFC821.  This method is thread 
		/// safe.
		/// </summary>
		public void HandleConnection(Socket socket)
		{
			long currentConnectionId;
			// Really only need to lock on the long, but that is not
			// allowed.  Is there a better way to do this?
			lock(this)
			{
				currentConnectionId = _connectionId++;
			}
			
			var context = new SmtpContext(currentConnectionId, socket, _logger);
			
			try 
			{
				SendWelcomeMessage(context);
				ProcessCommands(context);
			}
			catch(Exception exception)
			{
				_logger.Error(exception, "Connection {0}: Error: {1}", context.ConnectionId, exception);
			}
		}
		
		/// <summary>
		/// Sends the welcome greeting to the client.
		/// </summary>
		private void SendWelcomeMessage(SmtpContext context)
		{
			context.WriteLine(WelcomeMessage);
		}
		
		/// <summary>
		/// Handles the command input from the client.  This
		/// message returns when the client issues the quit command.
		/// </summary>
		private void ProcessCommands(SmtpContext context)
		{
			while(true)
			{
				try
				{
					var inputLine = context.ReadLine();
					if (inputLine == null)
					{
						context.Close();
						return;
					}

                    _logger.Debug("ProcessCommands Read: " + inputLine);
					var inputs = inputLine.Split(" ".ToCharArray());
					
					switch(inputs[0].ToLower())
					{
						case "helo":
							Helo(context, inputs);
							break;
						case "rset":
							Rset(context);
							break;
						case "noop":
							context.WriteLine(MessageOk);
							break;
						case "quit":
							context.WriteLine(MessageGoodbye);
							context.Close();
							return;
						case "mail":
							if (inputs[1].ToLower().StartsWith("from"))
							{
								Mail(context, inputLine.Substring(inputLine.IndexOf(" ")));
								break;
							}
							context.WriteLine(MessageUnknownCommand);
							break;
						case "rcpt":
							if (inputs[1].ToLower().StartsWith("to")) 							
							{
								Rcpt(context, inputLine.Substring(inputLine.IndexOf(" ")));
								break;
							}
							context.WriteLine(MessageUnknownCommand);
							break;
						case "data":
							Data(context);
							break;
						default:
							context.WriteLine(MessageUnknownCommand);
							break;
					}				
				}
				catch(Exception exception)
				{
                    _logger.Error(exception, "Connection {0}: Exception occured while processing commands: {1}", context.ConnectionId, exception);
					context.WriteLine(MessageSystemError);
				}
			}
		}

		/// <summary>
		/// Handles the HELO command.
		/// </summary>
		private void Helo(SmtpContext context, string[] inputs)
		{
			if (context.LastCommand == -1)
			{
				if (inputs.Length == 2)
				{
					context.ClientDomain = inputs[1];
					context.LastCommand = CommandHelo;
					context.WriteLine(HeloResponse);				
				}
				else
				{
					context.WriteLine(MessageInvalidArgumentCount);
				}
			}
			else
			{
				context.WriteLine(MessageInvalidCommandOrder);
			}
		}
		
		/// <summary>
		/// Reset the connection state.
		/// </summary>
		private static void Rset(SmtpContext context)
		{
			if (context.LastCommand != -1)
			{
				// Dump the message and reset the context.
				context.Reset();
				context.WriteLine(MessageOk);
			}
			else
			{
				context.WriteLine(MessageInvalidCommandOrder);
			}
		}
		
		/// <summary>
		/// Handle the MAIL FROM:&lt;address&gt; command.
		/// </summary>
		private void Mail(SmtpContext context, string argument)
		{
			if (context.LastCommand == CommandHelo)
			{
				MailAddress emailAddress;
				try
				{
                    emailAddress = new MailAddress(ParseAddress(argument));
				}
                catch(Exception)
                {
                    _logger.Debug("Connection {0}: MailFrom argument: {1} rejected.  Should be from:<username@domain.com>", context.ConnectionId, argument);
					context.WriteLine(MessageInvalidAddress);
					return;
				}
				context.MessageData.FromAddress = emailAddress;
				context.LastCommand = CommandMail;
				context.WriteLine(MessageOk);
                _logger.Debug("Connection {0}: MailFrom address: {1} accepted.", context.ConnectionId, emailAddress.ToString());
			}
			else
			{
				context.WriteLine(MessageInvalidCommandOrder);
			}
		}
		
		/// <summary>
		/// Handle the RCPT TO:&lt;address&gt; command.
		/// </summary>
		private void Rcpt(SmtpContext context, string argument)
		{
			if (context.LastCommand == CommandMail || context.LastCommand == CommandRcpt)
			{				
				var address = ParseAddress(argument);
				if (address != null)
				{
				    MailAddress emailAddress;
					try
					{
                        emailAddress = new MailAddress(address);
					}
					catch(FormatException)
					{
                        _logger.Debug("Connection {0}: RcptTo argument: {1} rejected.  Should be from:<username@domain.com>", context.ConnectionId, argument);
						context.WriteLine(MessageInvalidAddress);
					    return;
					}	
					
					// Check to make sure we want to accept this message.
					if (_recipientFilter(context, emailAddress))
					{						
						context.MessageData.AddToAddress(emailAddress);
						context.LastCommand = CommandRcpt;							
						context.WriteLine(MessageOk);
                        _logger.Debug("Connection {0}: RcptTo address: {1} accepted.", context.ConnectionId, address);
					}
					else
					{
						context.WriteLine(MessageUnknownUser);
                        _logger.Debug("Connection {0}: RcptTo address: {1} rejected.  Did not pass Address Filter.", context.ConnectionId, address);
					}
				}
				else
				{
                    _logger.Debug("Connection {0}: RcptTo argument: {1} rejected.  Should be from:<username@domain.com>", context.ConnectionId, argument);
					context.WriteLine(MessageInvalidAddress);
				}
			}
			else
			{
				context.WriteLine(MessageInvalidCommandOrder);
			}
		}
		
		private void Data(SmtpContext context)
		{
			context.WriteLine(MessageStartData);
			
			var messageData = context.MessageData;
			var clientEndPoint = (IPEndPoint) context.Socket.RemoteEndPoint;
			var header = new StringBuilder();
			header.Append(String.Format("Received: from {0} ({0} [{1}])\r\n", context.ClientDomain, clientEndPoint.Address));
			
			messageData.AddData(header.ToString());
			
			var line = context.ReadLine();
			while(!line.Equals("."))
			{
				messageData.AddData(line);
				messageData.AddData("\r\n");
				line = context.ReadLine();
			}

			// Spool the message
			_messageSpool.Queue(messageData.ParseMessage());
			context.WriteLine(MessageOk);
			
			// Reset the connection.
			context.Reset();
		}

		/// <summary>
		/// Parses a valid email address out of the input string and return it.
		/// Null is returned if no address is found.
		/// </summary>
		private static string ParseAddress(string input)
		{
		    return input.Split(new [] {':'}, 2).Last();
		}	
	}
}
