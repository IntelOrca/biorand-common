using System;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public class RouteFinderException(string message, object state) : Exception(message)
    {
        public object State => state;
    }
}
