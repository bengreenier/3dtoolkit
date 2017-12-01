using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebRtcWrapper.Signalling
{
	public class SimpleHttpClientException : Exception
	{
		public SimpleHttpClientException(Exception inner) : base(inner.Message, inner) { }
	}
}
