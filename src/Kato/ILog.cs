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
}