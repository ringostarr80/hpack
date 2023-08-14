using System;

namespace hpack
{
    public class HPackNullReferenceException : NullReferenceException
    {
        public HPackNullReferenceException()
        {
        }

        public HPackNullReferenceException(string message) : base(message)
        {
        }

        public HPackNullReferenceException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
