using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;

namespace Kato
{
	/// <summary>
	/// Stores an incoming SMTP Message.
	/// </summary>
	public class SmtpMessageData
	{
		private static readonly string DoubleNewline = Environment.NewLine + Environment.NewLine;
	    private const string SubjectHeader = "subject";
	    private const string SenderHeader = "sender";
	    private const string ReplyToHeader = "reply-to";
	    private const string ContentDispositionHeader = "content-disposition";
        private const string AttachmentDisposition = "attachment";
        private const string ContentTypeHeader = "content-type";
	    private const string ContentDispositionFileName = "filename";
        private const string ContentTypeBoundry = "boundary";
	    private const string ContentTypeMultiPartMixed = "multipart/mixed";
	    private const string ContentTypeMultiPartAlternative = "multipart/alternative";
        private const string ContentTypeHtml = "text/html";
	    private const string ContentTypeName = "name";
	    private const string ContentTransferEncodingHeader = "content-transfer-encoding";
	    private static readonly string[] StandardHeaders = { SubjectHeader, SenderHeader, ReplyToHeader };

        private readonly List<MailAddress> _recipientAddresses;
		private readonly StringBuilder _data;

		/// <summary>
		/// Creates a new message.
		/// </summary>
		public SmtpMessageData()
		{
            _recipientAddresses = new List<MailAddress>();
			_data = new StringBuilder();
		}

        public void AddData(string data)
        {
            _data.Append(data);
        }

	    /// <summary>
	    /// The email address of the person
	    /// that sent this email.
	    /// </summary>
        public MailAddress FromAddress { get; set; }
		
		/// <summary>Addes an address to the recipient list.</summary>
        public void AddToAddress(MailAddress address)
		{
			_recipientAddresses.Add(address);			
		}

        public MailMessage ParseMessage()
        {
            var headers = ParseHeaders(_data.ToString());
            
            var message = new MailMessage
            {
                From = headers.ContainsKey("from") ? 
                    new MailAddress(headers["from"].Value) : 
                    FromAddress
            };

            var to = headers.ContainsKey("to")
                ? ParseAddresses(headers["to"].Value)
                : Enumerable.Empty<MailAddress>().ToList();

            var cc = headers.ContainsKey("cc")
                ? ParseAddresses(headers["cc"].Value)
                : Enumerable.Empty<MailAddress>().ToList();

            if (to.Any() || cc.Any())
            {
                to.ForEach(x => message.To.Add(x));
                cc.ForEach(x => message.CC.Add(x));
                _recipientAddresses.Select(x => x.Address)
                    .Except(to.Select(x => x.Address)
                    .Concat(cc.Select(x => x.Address)))
                    .ToList().ForEach(x => message.Bcc.Add(x));
            }
            else _recipientAddresses.ForEach(x => message.To.Add(x));

            if (headers.ContainsKey(SubjectHeader)) message.Subject = headers[SubjectHeader].Value;
            if (headers.ContainsKey(SenderHeader)) message.Sender = new MailAddress(headers[SenderHeader].Value);
            if (headers.ContainsKey(ReplyToHeader)) message.ReplyToList.Add(new MailAddress(headers[ReplyToHeader].Value));

            foreach (var header in headers.Where(x => StandardHeaders.All(y => x.Key != y)))
                message.Headers[header.Key] = header.Value.RawValue;

            var parts = ParseMessageParts(_data.ToString(), headers);

            if (parts.Any())
            {
                var bodies = parts.Where(x => !x.Headers.ContainsKey(ContentDispositionHeader) ||
                                            x.Headers[ContentDispositionHeader].Value != AttachmentDisposition);
                if (bodies.Any())
                {
                    var body = bodies.First();
                    message.Body = !body.Headers.ContainsKey(ContentTransferEncodingHeader) ? body.Data : 
                        Encoding.UTF8.GetString(DecodeData(body.Data, body.Headers[ContentTransferEncodingHeader].Value));
                    message.IsBodyHtml = (body.Headers.ContainsKey(ContentTypeHeader) && body.Headers[ContentTypeHeader].Value == ContentTypeHtml) ||
                        (headers.ContainsKey(ContentTypeHeader) && headers[ContentTypeHeader].Value == ContentTypeHtml);
                }

                if (headers.ContainsKey(ContentTypeHeader) && headers[ContentTypeHeader].Value == ContentTypeMultiPartAlternative && bodies.Count() > 1)
                {
                    bodies.Skip(1).ToList().ForEach(x => message.AlternateViews.Add(
                        AlternateView.CreateAlternateViewFromString(x.Data, new ContentType(x.Headers[ContentTypeHeader].RawValue))));
                }

                parts.Where(x => x.Headers.ContainsKey(ContentDispositionHeader) && 
                        x.Headers[ContentDispositionHeader].Value == AttachmentDisposition)
                    .Select(x => new {
                        Data = new MemoryStream(DecodeData(x.Data, x.Headers[ContentTransferEncodingHeader].Value)),
                        Filename = x.Headers[ContentDispositionHeader].SubValues.ContainsKey(ContentDispositionFileName) ?
                            x.Headers[ContentDispositionHeader].SubValues[ContentDispositionFileName] : null,
                        Name = x.Headers[ContentTypeHeader].SubValues.ContainsKey(ContentTypeName) ?
                            x.Headers[ContentTypeHeader].SubValues[ContentTypeName] : null,
                        MediaType = x.Headers[ContentTypeHeader].RawValue })
                    .ToList().ForEach(x => message.Attachments.Add(new Attachment(x.Data, x.Filename ?? x.Name, x.MediaType)));
            }
            return message;
        }

        private static byte[] DecodeData(string data, string encoding)
        {
            switch (encoding)
            {
                case "base64": return Convert.FromBase64String(data);
                case "7bit":
                case "8bit":
                case "binary": return Encoding.ASCII.GetBytes(data);
                case "quoted-printable": return Encoding.UTF8.GetBytes(Attachment.CreateAttachmentFromString("", data).Name);
                default: throw new Exception($"Content transfer encoding of type {encoding} not supported.");
            }
        }

        private static IDictionary<string, Header> ParseHeaders(string data)
		{
            var headers = new Dictionary<string, Header>();

            var parts = Regex.Split(data, DoubleNewline);
			var headerString = parts[0] + DoubleNewline;

			var headerKeyCollectionMatch = Regex.Matches(headerString, @"^(?<key>\S*):", RegexOptions.Multiline);
		    foreach(Match headerKeyMatch in headerKeyCollectionMatch)
			{
			    var key = headerKeyMatch.Result("${key}");
                var valueMatch = Regex.Match(headerString, key + @":(?<value>.*?)\r\n[\S\r]", RegexOptions.Singleline);
			    if (!valueMatch.Success) continue;
			    var value = valueMatch.Result("${value}").Trim();
			    value = Regex.Replace(value, "\r\n", "");
			    value = Regex.Replace(value, @"\s+", " ");
			    var subValues = value.Contains(";") ?
                    value.Split(';').Skip(1).Select(x => x.Split(new [] {'='}, 2)).ToDictionary(x => x[0].Trim().ToLower(), x => x[1].Trim(new [] {'\"'}).Trim()) :
                    new Dictionary<string, string>();
			    key = key.ToLower().Trim();
			    headers[key] = new Header {
                        Name = key,
                        Value = value.Split(';').First(),
			            RawValue = value.Trim(),
                        SubValues = subValues
			        };
			}

			return headers;
		}

        private static List<MessagePart> ParseMessageParts(string data, IDictionary<string, Header> headers)
        {
			var messageParts = new List<MessagePart>();

	        if (headers.ContainsKey(ContentTypeHeader) && 
                (headers[ContentTypeHeader].Value == ContentTypeMultiPartMixed ||
                 headers[ContentTypeHeader].Value == ContentTypeMultiPartAlternative) && 
                headers[ContentTypeHeader].SubValues.ContainsKey(ContentTypeBoundry))
	        {
                var partRegex = new Regex(string.Format("--{0}(?<part>.*?)--{0}", 
                    Regex.Escape(headers[ContentTypeHeader].SubValues[ContentTypeBoundry])), RegexOptions.Singleline);
                Match match = partRegex.Match(data);
                while (match.Success)
                {
                    var parts = Regex.Split(match.Result("${part}").Trim(), DoubleNewline);
                    messageParts.Add(new MessagePart {
                            Headers = ParseHeaders(parts[0]),
                            Data = parts.Length > 1 ? parts[1].Trim() : null });
                    match = partRegex.Match(data, match.Index + 1);
                }
	        }
	        else
	        {
                var parts = Regex.Split(data, DoubleNewline);
	            if (parts.Length >= 2)
                    messageParts.Add(new MessagePart { Headers = headers, Data = parts[1].Trim() });
	        }
	        return messageParts;
        }

        private static readonly Regex AddressRegex = new Regex("(.*?<.+?@.+?>)(,)?", RegexOptions.IgnoreCase);

        private static List<MailAddress> ParseAddresses(string addresses)
        {
            return AddressRegex.Matches(addresses).Cast<Match>()
                .Select(x => new MailAddress(x.Groups[1].Value)).ToList();
        } 

        private class Header
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public string RawValue { get; set; }
            public Dictionary<string, string> SubValues { get; set; }
        }

        private class MessagePart
        {
            public IDictionary<string, Header> Headers { get; set; }
            public string Data { get; set; }
        }
    }
}
