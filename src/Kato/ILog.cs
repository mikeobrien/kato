using System;

namespace Kato
{
    public interface ILog
    {
        void Debug(string format, params object[] args);
        void Error(Exception exception, string format, params object[] args);
    }

    public class NullLogger : ILog
    {
        public void Debug(string format, params object[] args) { }
        public void Error(Exception exception, string format, params object[] args) { }
    }

    public class DebugLogger : ILog
    {
        public void Debug(string format, params object[] args)
        {
            Write(string.Format(format, args));
        }

        public void Error(Exception exception, string format, params object[] args)
        {
            Debug(format, args);
            Write(exception);
        }

        private void Write(object value)
        {
            System.Diagnostics.Debug.WriteLine("{0}: {1}", DateTime.Now, value);
        }
    }
}