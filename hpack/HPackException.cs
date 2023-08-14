using System;

namespace hpack
{
    public class HPackException : Exception
    {
        public HPackException()
        {
        }

        public HPackException(string message) : base(message)
        {
        }

        public HPackException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
