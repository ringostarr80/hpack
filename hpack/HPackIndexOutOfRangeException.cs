using System;

namespace hpack
{
    public class HPackIndexOutOfRangeException : Exception
    {
        public HPackIndexOutOfRangeException()
        {
        }

        public HPackIndexOutOfRangeException(string message) : base(message)
        {
        }

        public HPackIndexOutOfRangeException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
