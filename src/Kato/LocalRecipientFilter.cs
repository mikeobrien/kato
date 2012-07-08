namespace Kato
{
    /// <summary>
	/// Allows all email addresses addressed to the local domain specified
	/// in the constructor.
	/// </summary>	
	public class LocalRecipientFilter : IRecipientFilter {
		
		private readonly string _domain;
		
		/// <summary>
		/// Specifies the domain to accept email for.
		/// </summary>
		public LocalRecipientFilter( string domain ) 
		{
			_domain = domain.ToLower();
		}

		/// <summary>
		/// Accepts only local email.
		/// </summary>
		/// <param name='context'>The SMTPContext</param>
		/// <param name='recipient'>TODO - add parameter description</param>
		public virtual bool AcceptRecipient( SmtpContext context, EmailAddress recipient )
		{
			return _domain.Equals( recipient.Domain );
		}
	}
}
