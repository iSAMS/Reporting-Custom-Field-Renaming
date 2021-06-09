using System;
using System.Runtime.Serialization;

namespace iSAMS.Utilities.Reporting.CustomFieldRenaming.Exceptions
{
    /// <inheritdoc />
    [Serializable]
    public class RestApiException : ApplicationException
    {
        /// <summary>Initializes a new instance of the <see cref="T:System.RestApiException" /> class with a specified error message.</summary>
        /// <param name="apiPath">The URI of the request.</param>
        /// <param name="message">A message that describes the error.</param>
        public RestApiException(string apiPath, string message)
            : base(message)
        {
            ApiPath = apiPath;
        }

        /// <summary>Initializes a new instance of the <see cref="T:System.RestApiException" /> class with a specified error message and a reference to the inner exception that is the cause of this exception.</summary>
        /// <param name="apiPath">The URI of the request.</param>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.If the <paramref name="innerException" /> parameter is not a null reference, the current exception is raised in a <see langword="catch" /> block that handles the inner exception. </param>
        public RestApiException(string apiPath, string message, Exception innerException)
            : base(message, innerException)
        {
            ApiPath = apiPath;
        }

        /// <summary>Initializes a new instance of the <see cref="T:System.RestApiException" /> class with serialized data.</summary>
        /// <param name="info">The object that holds the serialized object data.</param>
        /// <param name="context">The contextual information about the source or destination.</param>
        protected RestApiException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public string ApiPath { get; }
    }
}