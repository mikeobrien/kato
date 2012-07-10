using System;
using System.Collections.Concurrent;
using System.Net.Mail;

namespace Kato
{
    /// <summary>
	/// Provides a memory based IMessageSpool.
	/// </summary>
	public class MemoryMessageSpool : IMessageSpool
	{
		private readonly BlockingCollection<MailMessage> _queue;

		/// <summary>
		/// Initializes the queue.
		/// </summary>
		public MemoryMessageSpool()
		{
            _queue = new BlockingCollection<MailMessage>();
		}

		/// <summary>
		/// Addes the message to the in memory queue.
		/// </summary>
		/// <param name='message'>The message to queue.</param>
		public virtual bool Queue(MailMessage message)
		{
            _queue.TryAdd(message, TimeSpan.FromSeconds(5));
			return true;
		}
		
		/// <summary>Returns the oldest message in the spool.</summary>
		public virtual MailMessage Dequeue(int timeout = 5)
        { 
		    MailMessage message;
		    _queue.TryTake(out message, TimeSpan.FromSeconds(timeout));
			return message;
		}
	}
}
