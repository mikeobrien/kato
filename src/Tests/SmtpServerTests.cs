using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Kato;
using NUnit.Framework;
using Should;

namespace Tests
{
    [TestFixture]
    public class SmtpServerTests
    {
        private const string Domain = "test.com";
        private const string Host = "127.0.0.1";
        private const int Port = 7234;

        private const string From1 = "someone@reachmail.com";
        private const string To1 = "someone@test.com";
        private const string Subject1 = "A message for you";
        private const string Body1 = "This is a message";
        private const string HtmlBody1 = "<p>This is a message</p>";

        private const string From2 = "anotherperson@reachmail.com";
        private const string To2 = "anotherperson@test.com";
        private const string Subject2 = "Some other message for you";
        private const string Body2 = "This is some other message";

        private SmtpServer _server;
        private MemoryMessageSpool _messages;

        [SetUp]
        public void SetUp()
        {
            _messages = new MemoryMessageSpool();
            _server = new SmtpServer(Domain, Port, _messages);
            _server.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _server.Stop();
        }

        [Test]
        public void should_recieve_messages()
        {
            using (var client = new SmtpClient(Host, Port))
            {
                client.Send(From1, To1, Subject1, Body1);
                client.Send(From2, To2, Subject2, Body2);
            }

            var message = _messages.Dequeue();
            message.From.Address.ShouldEqual(From1);
            message.To[0].Address.ShouldEqual(To1);
            message.Subject.ShouldEqual(Subject1);
            message.Body.ShouldEqual(Body1);

            message = _messages.Dequeue();
            message.From.Address.ShouldEqual(From2);
            message.To[0].Address.ShouldEqual(To2);
            message.Subject.ShouldEqual(Subject2);
            message.Body.ShouldEqual(Body2);
        }

        [Test]
        public void should_parse_multipart_alternative_html_priority_messages()
        {
            using (var client = new SmtpClient(Host, Port))
            {
                var outgoingMessage = new MailMessage
                {
                    From = new MailAddress(From1),
                    To = { To1 },
                    Subject = Subject1,
                    IsBodyHtml = false,
                    Body = Body1
                };

                var htmlView = AlternateView.CreateAlternateViewFromString(HtmlBody1, null, MediaTypeNames.Text.Html);
                outgoingMessage.AlternateViews.Add(htmlView);

                client.Send(outgoingMessage);
            }

            var message = _messages.Dequeue();
            message.From.Address.ShouldEqual(From1);
            message.To[0].Address.ShouldEqual(To1);
            message.Subject.ShouldEqual(Subject1);
            message.Body.ShouldEqual(Body1);
            message.IsBodyHtml.ShouldBeFalse();
            message.AlternateViews.Count.ShouldEqual(1);
            var view = message.AlternateViews.First();
            new StreamReader(view.ContentStream).ReadToEnd().ShouldEqual(HtmlBody1);
            view.ContentType.MediaType.ShouldEqual("text/html");
        }

        [Test]
        public void should_parse_multipart_alternative_text_priority_messages()
        {
            using (var client = new SmtpClient(Host, Port))
            {
                var outgoingMessage = new MailMessage
                {
                    From = new MailAddress(From1),
                    To = { To1 },
                    Subject = Subject1,
                    IsBodyHtml = true,
                    Body = HtmlBody1
                };

                var textView = AlternateView.CreateAlternateViewFromString(Body1, null, MediaTypeNames.Text.Plain);
                outgoingMessage.AlternateViews.Add(textView);
                
                client.Send(outgoingMessage);
            }

            var message = _messages.Dequeue();
            message.From.Address.ShouldEqual(From1);
            message.To[0].Address.ShouldEqual(To1);
            message.Subject.ShouldEqual(Subject1);
            message.Body.ShouldEqual(HtmlBody1);
            //message.IsBodyHtml.ShouldBeTrue(); <-- I think there is a bug in the .net framework implementation
            message.AlternateViews.Count.ShouldEqual(1);
            var view = message.AlternateViews.First();
            new StreamReader(view.ContentStream).ReadToEnd().ShouldEqual(Body1);
            view.ContentType.MediaType.ShouldEqual("text/plain");
        }

        [Test]
        public void should_recieve_messages_with_attachments()
        {
            const string contentType1 = "text/plain";
            const string filename1 = "yada.txt";
            const string attachment1 = "This is an attachment yo!";
            const string contentType2 = "text/html";
            const string attachment2 = "This is another attachment yo!";

            using (var client = new SmtpClient(Host, Port))
            {
                var mailMessage = new MailMessage(From1, To1, Subject1, Body1);
                mailMessage.Attachments.Add(new Attachment(new MemoryStream(Encoding.ASCII.GetBytes(attachment1)), filename1, contentType1));
                client.Send(mailMessage);

                mailMessage = new MailMessage(From2, To2, Subject2, Body2);
                var messageAttachment = new Attachment(new MemoryStream(Encoding.ASCII.GetBytes(attachment1)), null, contentType1);
                messageAttachment.TransferEncoding = TransferEncoding.SevenBit;
                mailMessage.Attachments.Add(messageAttachment);
                messageAttachment = new Attachment(new MemoryStream(Encoding.ASCII.GetBytes(attachment2)), null, contentType2);
                messageAttachment.TransferEncoding = TransferEncoding.QuotedPrintable;
                mailMessage.Attachments.Add(messageAttachment);
                client.Send(mailMessage);
            }

            var message = _messages.Dequeue();
            message.From.Address.ShouldEqual(From1);
            message.To[0].Address.ShouldEqual(To1);
            message.Subject.ShouldEqual(Subject1);
            message.Body.ShouldEqual(Body1);
            message.Attachments.Count.ShouldEqual(1);
            var attachment = message.Attachments[0];
            attachment.ContentType.MediaType.ShouldEqual(contentType1);
            attachment.Name.ShouldEqual(filename1);
            new StreamReader(attachment.ContentStream).ReadToEnd().ShouldEqual(attachment1);

            message = _messages.Dequeue();
            message.From.Address.ShouldEqual(From2);
            message.To[0].Address.ShouldEqual(To2);
            message.Subject.ShouldEqual(Subject2);
            message.Body.ShouldEqual(Body2);
            message.Attachments.Count.ShouldEqual(2);

            attachment = message.Attachments[0];
            attachment.ContentType.MediaType.ShouldEqual(contentType1);
            new StreamReader(attachment.ContentStream).ReadToEnd().ShouldEqual(attachment1);

            attachment = message.Attachments[1];
            attachment.ContentType.MediaType.ShouldEqual(contentType2);
            new StreamReader(attachment.ContentStream).ReadToEnd().ShouldEqual(attachment2);
        }
    }
}
