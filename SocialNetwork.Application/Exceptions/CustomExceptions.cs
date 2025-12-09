using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Exceptions
{
    public class CustomExceptions : Exception
    {
        public class BadRequestException : Exception
        {
            public BadRequestException(string message) : base(message) { }
        }

        public class NotFoundException : Exception
        {
            public NotFoundException(string message) : base(message) { }
        }

        public class UnauthorizedException : Exception
        {
            public UnauthorizedException(string message) : base(message) { }
        }

        public class ForbiddenException : Exception
        {
            public ForbiddenException(string message) : base(message) { }
        }

        public class InternalServerException : Exception
        {
            public InternalServerException(string message) : base(message) { }
        }
    }
}
