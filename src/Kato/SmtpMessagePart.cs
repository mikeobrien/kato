using System;
using System.Collections;
using System.Text.RegularExpressions;

namespace Kato
{
	/// <summary>
	/// Stores a single part of a multipart message.
	/// </summary>
	public class SmtpMessagePart
	{
		private static readonly string DoubleNewline = Environment.NewLine + Environment.NewLine;

		private Hashtable _headerFields;
		private readonly string _headerData = String.Empty;
		private readonly string _bodyData = String.Empty;

		/// <summary>
		/// Creates a new message part.  The input string should be the body of 
		/// the attachment, without the "------=_NextPart" separator strings.
		/// The last 4 characters of the data will be "\r\n\r\n".
		/// </summary>
		public SmtpMessagePart( string data )
		{
			string[] parts = Regex.Split( data, DoubleNewline );

			_headerData = parts[0] + DoubleNewline;
			_bodyData = parts[1] + DoubleNewline;					
		}

		/// <summary>
		/// A hash table of all the Headers in the email message.  They keys
		/// are the header names, and the values are the assoicated values, including
		/// any sub key/value pairs is the header.
		/// </summary>
		public Hashtable Headers
		{
			get { return _headerFields ?? (_headerFields = SmtpMessage.ParseHeaders(_headerData)); }
		}
		/// <summary>
		/// The raw text that represents the header of the mime part.
		/// </summary>
		public string HeaderData
		{
			get { return _headerData; }
		}

		/// <summary>
		/// The raw text that represents the actual mime part.
		/// </summary>
		public string BodyData
		{
			get { return _bodyData; }
		}
	}
}
