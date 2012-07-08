using System.Collections;

namespace Kato
{
    /// <summary>
	/// Provides a memory based IMessageSpool.
	/// </summary>
	public class MemoryMessageSpool : IMessageSpool
	{
		private readonly Queue _queue;

		/// <summary>
		/// Initializes the queue.
		/// </summary>
		public MemoryMessageSpool()
		{
			_queue = new Queue();
		}

		/// <summary>
		/// Addes the message to the in memory queue.
		/// </summary>
		/// <param name='message'>The message to queue.</param>
		public virtual bool SpoolMessage(SmtpMessage message)
		{
			_queue.Enqueue( message );
			return true;
		}
		
		/// <summary>Returns the oldest message in the spool.</summary>
		public virtual SmtpMessage NextMessage()
		{
			return (SmtpMessage) _queue.Dequeue();
		}
		
		/// <summary>Removes any messages from the spool.</summary>
		public virtual void ClearSpool()
		{
			_queue.Clear();
		}
	}
}
