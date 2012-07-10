using System;
using System.Diagnostics;
using System.IO;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using Kato;
using NUnit.Framework;
using Should;

namespace Tests
{
	[TestFixture]
	public class SmtpMessageTests
	{
		private const string Attachment1HeaderData = 
			"Content-Type: application/pdf;\r\n" +
			"	name=\"000000001.pdf\"\r\n" +
			"Content-Transfer-Encoding: base64\r\n" +
			"Content-Disposition: attachment;\r\n" +
			"	filename=\"000000001.pdf\"\r\n" +
			"\r\n";

        private const string Attachment1BodyDataEncoded = 
			"JVBERi0xLjIKekdf1fnfSqQYt7AjczYfpmRSIEyEcx8KMSAwIG9iago8PAovVHlwZSAvQ2F0YWxv\r\n" +
			"ZwovUGFnZXMgMyAwIFIKL091dGxpbmVzIDIgMCBSCj4+CmVuZG9iagoyIDAgb2JqCjw8Ci9UeXBl\r\n" +
			"IC9PdXRsaW5lcwovQ291bnQgMQovRmlyc3QgOCAwIFIKL0xhc3QgOCAwIFIKPj4KZW5kb2JqCjMg\r\n" +
			"MCBvYmoKPDwKL1R5cGUgL1BhZ2VzCi9Db3VudCAxCi9LaWRzIFsgNSAwIFIgXQo+PgplbmRvYmoK\r\n" +
			"NCAwIG9iagpbL1BERiAvVGV4dCAvSW1hZ2VCIF0KZW5kb2JqCjUgMCBvYmoKPDwKL1R5cGUgL1Bh\r\n" +
			"Z2UKL1BhcmVudCAzIDAgUgovUmVzb3VyY2VzIDw8Ci9YT2JqZWN0IDw8Ci9JTWNwZGYwIDcgMCBS\r\n" +
			"Cj4+Ci9Qcm9jU2V0IDQgMCBSID4+Ci9NZWRpYUJveCBbMCAwIDM5OSA1NjFdCi9Dcm9wQm94IFsw\r\n" +
			"IDAgMzk5IDU2MV0KL1JvdGF0ZSAwCi9Db250ZW50cyA2IDAgUgo+PgplbmRvYmoKNiAwIG9iago8\r\n" +
			"PAovTGVuZ3RoIDQ3Ci9GaWx0ZXIgWy9GbGF0ZURlY29kZV0KPj4Kc3RyZWFtCnic4yrkMtQzAAIF\r\n" +
			"AxQKq2ByLpexpSU2GVMzXBr0PX2TC1LSDBRc8rkCuQCBJhXPCmVuZHN0cmVhbQplbmRvYmoKNyAw\r\n" +
			"IG9iago8PAovVHlwZSAvWE9iamVjdAovU3VidHlwZSAvSW1hZ2UKL05hbWUgL0lNY3BkZjAKL1dp\r\n" +
			"ZHRoIDE2NjQKL0hlaWdodCAyMzM4Ci9GaWx0ZXIgL0ZsYXRlRGVjb2RlCi9CaXRzUGVyQ29tcG9u\r\n" +
			"ZW50IDEKL0NvbG9yU3BhY2UgL0RldmljZUdyYXkKL0xlbmd0aCAyNTAxMgo+PgpzdHJlYW0KeJzs\r\n" +
			"vV1sJEl+4BfZ2epsnSjmyGvguBBV0cIKXr+Z47HhGlxtxQgyTjBwwD74xfCXejyG51FsD2xVQ6WK\r\n" +
			"\r\n";

	    private readonly static byte[] Attachment1BodyData = Convert.FromBase64String(Attachment1BodyDataEncoded);

	    private const string Attachment2BodyDataEncoded =
            "PAovTGVuZ3RoIDQ3Ci9GaWx0ZXIgWy9GbGF0ZURlY29kZV0KPj4Kc3RyZWFtCnic4yrkMtQzAAIF\r\n" +
            "AxQKq2ByLpexpSU2GVMzXBr0PX2TC1LSDBRc8rkCuQCBJhXPCmVuZHN0cmVhbQplbmRvYmoKNyAw\r\n" +
            "IG9iago8PAovVHlwZSAvWE9iamVjdAovU3VidHlwZSAvSW1hZ2UKL05hbWUgL0lNY3BkZjAKL1dp\r\n";

        private readonly static byte[] Attachment2BodyData = Convert.FromBase64String(Attachment2BodyDataEncoded);

        private const string TestSingleBase64 = 
			"Received: from development02 (development02 [127.0.0.1])\r\n" +
			"     by adexs.com (Eric Daugherty's C# Email Server)\r\n" +
			"     4/16/2004 10:35:58 AM\r\n" +
			"From: \"Eric Daugherty\" <edaugherty@adexs.com>\r\n" +
			"To: <test@test.com>\r\n" +
			"Subject: CofAs\r\n" +
			"Date: Fri, 16 Apr 2004 10:35:55 -0500\r\n" +
			"Message-ID: <LIEBLGPCEJMNGHIPABGIGEABCAAA.edaugherty@adexs.com>\r\n" +
			"MIME-Version: 1.0\r\n" +
			"Content-Type: multipart/mixed;\r\n" +
			"	boundary=\"----=_NextPart_000_0000_01C4239E.999350F0\"\r\n" +
			"X-Priority: 3 (Normal)\r\n" +
			"X-MSMail-Priority: Normal\r\n" +
			"X-Mailer: Microsoft Outlook IMO, Build 9.0.2416 (9.0.2911.0)\r\n" +
			"Importance: Normal\r\n" +
			"X-MimeOLE: Produced By Microsoft MimeOLE V6.00.2800.1409\r\n" +
			"\r\n" +
			"This is a multi-part message in MIME format.\r\n" +
			"\r\n" +
			"------=_NextPart_000_0000_01C4239E.999350F0\r\n" +
			Attachment1HeaderData +
			Attachment1BodyDataEncoded +
			"------=_NextPart_000_0000_01C4239E.999350F0--\r\n" +
            "\r\n";

        private const string TestDoubleBase64 = 
			"Received: from development02 (development02 [127.0.0.1])\r\n" +
			"     by adexs.com (Eric Daugherty's C# Email Server)\r\n" +
			"     4/16/2004 10:35:58 AM\r\n" +
			"From: \"Eric Daugherty\" <edaugherty@adexs.com>\r\n" +
			"To: <test@test.com>\r\n" +
			"Subject: CofA\r\n" +
			"Date: Fri, 16 Apr 2004 10:35:55 -0500\r\n" +
			"Message-ID: <LIEBLGPCEJMNGHIPABGIGEABCAAA.edaugherty@adexs.com>\r\n" +
			"MIME-Version: 1.0\r\n" +
			"Content-Type: multipart/mixed;\r\n" +
			"	boundary=\"----=_NextPart_000_0000_01C4239E.999350F0\"\r\n" +
			"X-Priority: 3 (Normal)\r\n" +
			"X-MSMail-Priority: Normal\r\n" +
			"X-Mailer: Microsoft Outlook IMO, Build 9.0.2416 (9.0.2911.0)\r\n" +
			"Importance: Normal\r\n" +
			"X-MimeOLE: Produced By Microsoft MimeOLE V6.00.2800.1409\r\n" +
			"\r\n" +
			"This is a multi-part message in MIME format.\r\n" +
			"\r\n" +
			"------=_NextPart_000_0000_01C4239E.999350F0\r\n" +
			Attachment1HeaderData +
			Attachment1BodyDataEncoded +
			"------=_NextPart_000_0000_01C4239E.999350F0\r\n" +
			"Content-Type: application/pdf;\r\n" +
            "	name=\"000000002.pdf\"\r\n" +
			"Content-Transfer-Encoding: base64\r\n" +
			"Content-Disposition: attachment;\r\n" +
            "	filename=\"000000002.pdf\"\r\n" +
			"\r\n" +
            Attachment2BodyDataEncoded +
			"\r\n" +
			"------=_NextPart_000_0000_01C4239E.999350F0--\r\n" +
			"\r\n";

        private const string TestBodyBase64 = 
			"Received: from development02 (development02 [127.0.0.1])\r\n" +
			"     by adexs.com (Eric Daugherty's C# Email Server)\r\n" +
			"     4/22/2004 4:36:14 PM\r\n" +
			"From: \"Eric Daugherty\" <edaugherty@adexs.com>\r\n" +
			"To: <cc_1000@test.com>\r\n" +
			"Subject: CofAs\r\n" +
			"Date: Thu, 22 Apr 2004 16:36:14 -0500\r\n" +
			"Message-ID: <LIEBLGPCEJMNGHIPABGIKEAHCAAA.edaugherty@adexs.com>\r\n" +
			"MIME-Version: 1.0\r\n" +
			"Content-Type: application/pdf;\r\n" +
			"	name=\"000000002.pdf\"\r\n" +
			"Content-Transfer-Encoding: base64\r\n" +
			"Content-Disposition: attachment;\r\n" +
			"	filename=\"000000002.pdf\"\r\n" +
			"X-Priority: 3 (Normal)\r\n" +
			"X-MSMail-Priority: Normal\r\n" +
			"X-Mailer: Microsoft Outlook IMO, Build 9.0.2416 (9.0.2911.0)\r\n" +
			"Importance: Normal\r\n" +
			"X-MimeOLE: Produced By Microsoft MimeOLE V6.00.2800.1409\r\n" +
            "\r\n" +
			Attachment1BodyDataEncoded;

		[Test]
		public void MessageHeaders()
		{
            var messageData = new SmtpMessageData { FromAddress = new MailAddress("test@test.com") };
			messageData.AddData(TestSingleBase64);
		    var message = messageData.ParseMessage();

            message.Headers["Received"].ShouldEqual("from development02 (development02 [127.0.0.1]) by adexs.com (Eric Daugherty's C# Email Server) 4/16/2004 10:35:58 AM");
            message.Headers["From"].ShouldEqual("\"Eric Daugherty\" <edaugherty@adexs.com>");
            message.Subject.ShouldEqual("CofAs");
            message.Headers["Date"].ShouldEqual("Fri, 16 Apr 2004 10:35:55 -0500");
            message.Headers["X-MimeOLE"].ShouldEqual("Produced By Microsoft MimeOLE V6.00.2800.1409");
		}

		[Test]
		public void SingleBase64Attachment()
		{
            var messageData = new SmtpMessageData { FromAddress = new MailAddress("test@test.com") };
            messageData.AddData(TestSingleBase64);
            var message = messageData.ParseMessage();

            message.Attachments.Count.ShouldEqual(1);
            new BinaryReader(message.Attachments[0].ContentStream).ReadBytes((int)message.Attachments[0].ContentStream.Length).ShouldEqual(Attachment1BodyData);
            message.Attachments[0].ContentType.MediaType.ShouldEqual("application/pdf");
            message.Attachments[0].ContentDisposition.DispositionType.ShouldEqual("attachment");
            message.Attachments[0].Name.ShouldEqual("000000001.pdf");
		}

		[Test]
		public void DoubleBase64Attachment()
        {
            var messageData = new SmtpMessageData { FromAddress = new MailAddress("test@test.com") };
            messageData.AddData(TestDoubleBase64);
            var message = messageData.ParseMessage();

            message.Attachments.Count.ShouldEqual(2);
            new BinaryReader(message.Attachments[0].ContentStream).ReadBytes((int)message.Attachments[0].ContentStream.Length).ShouldEqual(Attachment1BodyData);
            message.Attachments[0].ContentType.MediaType.ShouldEqual("application/pdf");
            message.Attachments[0].ContentDisposition.DispositionType.ShouldEqual("attachment");
            message.Attachments[0].Name.ShouldEqual("000000001.pdf");

            new BinaryReader(message.Attachments[1].ContentStream).ReadBytes((int)message.Attachments[1].ContentStream.Length).ShouldEqual(Attachment2BodyData);
            message.Attachments[1].ContentType.MediaType.ShouldEqual("application/pdf");
            message.Attachments[1].ContentDisposition.DispositionType.ShouldEqual("attachment");
            message.Attachments[1].Name.ShouldEqual("000000002.pdf");
		}

		[Test]
		public void BodyBase64()
        { 
            var messageData = new SmtpMessageData { FromAddress = new MailAddress("test@test.com") };
            messageData.AddData(TestBodyBase64);
            var message = messageData.ParseMessage();

            message.Attachments.Count.ShouldEqual(1);
            new BinaryReader(message.Attachments[0].ContentStream).ReadBytes((int)message.Attachments[0].ContentStream.Length).ShouldEqual(Attachment1BodyData);
            message.Attachments[0].ContentType.MediaType.ShouldEqual("application/pdf");
            message.Attachments[0].ContentDisposition.DispositionType.ShouldEqual("attachment");
            message.Attachments[0].Name.ShouldEqual("000000002.pdf");
		}		
	}
}
