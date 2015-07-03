using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Kato;
using NUnit.Framework;
using Should;

namespace Tests
{
	[TestFixture]
	public class SmtpHandlerTests
	{
		private static readonly IPEndPoint EndPoint = new IPEndPoint(IPAddress.Loopback, 9900);
		private TcpListener _listener;
		private Queue<MailMessage> _messageSpool;
		
		[SetUp]
		public void Setup()
		{
			_messageSpool = new Queue<MailMessage>();
			var listener = new Thread(Listener) {IsBackground = true};
		    listener.Start();
			// Block for a second to make sure the socket gets started.
			Thread.Sleep(1000);
		}

		private void Listener()
		{
			try
			{
			    var domain = "testdomain.com";
				var processor = new SmtpHandler(domain, x => _messageSpool.Enqueue(x), (context, address) => address.Host == domain, new NullLogger());
				
				_listener = new TcpListener(EndPoint);
				_listener.Start();
				Console.WriteLine("Socket listener started...");
				var clientSocket = _listener.AcceptSocket();				
				processor.HandleConnection(clientSocket);
			}
			catch(Exception exception)
			{
				Console.WriteLine("Exception in Listener: " + exception);
				Console.WriteLine(exception.StackTrace);
			}
		}
		
		[TearDown]
		public void Teardown()
		{
			_listener.Stop();
		}
		
		[Test]
		public void BasicConnectionTest()
		{
			var socket = Connect();
			Disconnect(socket);						
		}
		
		[Test]
		public void MailFromAddressParsingTest()
		{
			var socket = Connect();
			
			CheckResponse(socket, "mail from:<user@name@domain.com>", "451");
			
			CheckResponse(socket, "mail from:<username@domain123.com>", "250");
						
			Disconnect(socket);
		}
		
		[Test]
		public void RcptToAddressParsingTest()
		{
			var socket = Connect();
			
			CheckResponse(socket, "mail from:<username@domain123.com>", "250");
			
			CheckResponse(socket, "rcpt to:<user@name@domain.com>", "451");
			
			CheckResponse(socket, "rcpt to:<username@domain123.com>", "550");
			CheckResponse(socket, "rcpt to:<username@domain.com>", "550");
			
			CheckResponse(socket, "rcpt to:<username@testdomain.com>", "250");
			CheckResponse(socket, "rcpt to:<user_100@testdomain.com>", "250");
			
			Disconnect(socket);	
		}
		
		[Test]
		public void DataTest()
		{
			var socket = Connect();
			CheckResponse(socket, "mail from:<username@domain123.com>", "250");
			CheckResponse(socket, "rcpt to:<username@testdomain.com>", "250");
			CheckResponse(socket, "data", "354");
			
			WriteLine(socket, "Date: Tue, 4 Nov 2003 10:13:27 -0600 (CST)");
			WriteLine(socket, "Subject: Test");
			WriteLine(socket, "");
			WriteLine(socket, "Message Body.");
			
			CheckResponse(socket, ".", "250");
			
			Disconnect(socket);
			
			var message = _messageSpool.Dequeue();
			
			Console.WriteLine("Message Recieved: ");
		}
			
		private Socket Connect()
		{
			Console.WriteLine("Connecting...");
			var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			socket.Connect(EndPoint); 
			
			// Read Welcome Message
			var line = ReadLine(socket);
            line.ShouldStartWith("220");
			
			// Helo
			WriteLine(socket, "helo nunittestdomain.com");
			line = ReadLine(socket);
            line.ShouldEqual("250 testdomain.com");
			
			return socket;
		}
		
		private void Disconnect(Socket socket)
		{			
			// Quit
			WriteLine(socket, "quit");
			var line = ReadLine(socket);
            line.ShouldStartWith("221");
			
			socket.Close();
		}
		
		private void CheckResponse(Socket socket, string command, string responseCode)
		{
			var line = WriteAndRead(socket, command);
            line.ShouldStartWith(responseCode);			
		}
		
		/// <summary>Helper method to combine a write and a read.</summary>
		public string WriteAndRead(Socket socket, string data)
		{
			WriteLine(socket, data);
			return ReadLine(socket);
		}
		
		/// <summary>
		/// Writes the string to the socket as an entire line.  This
		/// method will append the end of line characters, so the data
		/// parameter should not contain them.
		/// </summary>
		/// <param name="socket">The socket to write to.</param>
		/// <param name="data>The data to write the the client.</param>
		public void WriteLine(Socket socket, string data)
		{
			System.Console.WriteLine("Wrote: " + data);
			socket.Send(Encoding.ASCII.GetBytes(data + "\r\n"));
		}
		
		/// <summary>
		/// Reads an entire line from the socket.  This method
		/// will block until an entire line has been read.
		/// </summary>
		/// <param name="socket"></param>
		public String ReadLine(Socket socket)
		{
			var inputBuffer = new byte[80];
		    var inputString = new StringBuilder();
			string currentValue;

			// Read from the socket until an entire line has been read.			
			do
			{
				// Read the input data.
				var count = socket.Receive(inputBuffer);
				
				inputString.Append(Encoding.ASCII.GetString(inputBuffer, 0, count));
				currentValue = inputString.ToString();				
			}
			while(currentValue.IndexOf("\r\n") == -1);
						
			// Strip off EOL.
			currentValue = currentValue.Remove(currentValue.IndexOf("\r\n"), 2);
						
			Console.WriteLine("Read Line: " + currentValue);
			return currentValue;
		}
	}
}
