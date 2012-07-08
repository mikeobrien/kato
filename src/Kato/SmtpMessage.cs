using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;

namespace Kato
{
	/// <summary>
	/// Stores an incoming SMTP Message.
	/// </summary>
	public class SmtpMessage: object
	{
		private static readonly string DoubleNewline = Environment.NewLine + Environment.NewLine;

	    private readonly ArrayList _recipientAddresses;
		private readonly StringBuilder _data;
		
		private Hashtable _headerFields;

		/// <summary>
		/// Creates a new message.
		/// </summary>
		public SmtpMessage()
		{
			_recipientAddresses = new ArrayList();
			_data = new StringBuilder();
		}

		/// <summary>
		/// A hash table of all the Headers in the email message.  They keys
		/// are the header names, and the values are the assoicated values, including
		/// any sub key/value pairs is the header.
		/// </summary>
		public Hashtable Headers
		{
		    get { return _headerFields ?? (_headerFields = ParseHeaders(_data.ToString())); }
		}

	    /// <summary>
	    /// The email address of the person
	    /// that sent this email.
	    /// </summary>
	    public EmailAddress FromAddress { get; set; }

	    /// <summary>
		/// The addresses that this message will be
		/// delivered to.
		/// </summary>
		public EmailAddress[] ToAddresses
		{
			get
			{
				return (EmailAddress[]) _recipientAddresses.ToArray( typeof( EmailAddress ) );
			}
		}
		
		/// <summary>Addes an address to the recipient list.</summary>
		public void AddToAddress( EmailAddress address )
		{
			_recipientAddresses.Add( address );			
		}
		
		/// <summary>Message data.</summary>
		public string Data
		{
			get
			{
				return _data.ToString();
			}
		}
		
		/// <summary>Append data to message data.</summary>
		public void AddData( String data )
		{
			_data.Append( data );
		}
		
		/// <summary>
		/// Parses the message body and creates an Attachment object
		/// for each attachment in the message.
		/// </summary>
		public SmtpMessagePart[] MessageParts
		{
            get
            {
                return ParseMessageParts();
            }
		}
		
		/// <summary>
		/// Parses an entire message or message part and returns the header entries
		/// as a hashtable.
		/// </summary>
		/// <param name="partData">The raw message or message part data.</param>
		/// <returns>A hashtable of the header keys and values.</returns>
		internal static Hashtable ParseHeaders( string partData )
		{
			var headerFields = new Hashtable();
			
			var parts = Regex.Split( partData, DoubleNewline );
			var headerString = parts[0] + DoubleNewline;

			var headerKeyCollectionMatch = Regex.Matches( headerString, @"^(?<key>\S*):", RegexOptions.Multiline );
		    foreach( Match headerKeyMatch in headerKeyCollectionMatch )
			{
			    var headerKey = headerKeyMatch.Result( "${key}" );
                var valueMatch = Regex.Match( headerString, headerKey + @":(?<value>.*?)\r\n[\S\r]", RegexOptions.Singleline );
			    if (!valueMatch.Success) continue;
			    string headerValue = valueMatch.Result( "${value}" ).Trim();
			    headerValue = Regex.Replace( headerValue, "\r\n", "" );
			    headerValue = Regex.Replace( headerValue, @"\s+", " " );
			    // TODO: Duplicate headers (like Received) will be overwritten by the 'last' value.					
			    headerFields[ headerKey] = headerValue;
			}

			return headerFields;
		}

        private SmtpMessagePart[] ParseMessageParts()
        {
	        var message = _data.ToString();
	        var contentType = (string) Headers["Content-Type"];

	        // Check to see if it is a Multipart Messages
	        if( contentType != null && Regex.Match( contentType, "multipart/mixed", RegexOptions.IgnoreCase ).Success )
	        {
		        // Message parts are seperated by boundries.  Parse out what the boundry is so we can easily
		        // parse the parts out of the message.
		        var boundryMatch = Regex.Match( contentType, "boundary=\"(?<boundry>\\S+)\"", RegexOptions.IgnoreCase );
		        if( boundryMatch.Success )
		        {
			        var boundry = boundryMatch.Result( "${boundry}" );

			        var messageParts = new ArrayList();

			        //TODO Improve this Regex.
			        var matches = Regex.Matches( message, "--" + boundry + ".*\r\n" );

			        var lastIndex = -1;
			        foreach( Match match in matches )
			        {
				        var currentIndex = match.Index;
				        var matchLength = match.Length;
				
				        if( lastIndex != -1 )
				        {
					        var messagePartText = message.Substring( lastIndex, currentIndex - lastIndex );
					        messageParts.Add( new SmtpMessagePart( messagePartText ) );
				        }

				        lastIndex = currentIndex + matchLength;
			        }
			
			        return (SmtpMessagePart[]) messageParts.ToArray( typeof( SmtpMessagePart ) );
		        }
	        }
	        return new SmtpMessagePart[0];
        }
    }
}
