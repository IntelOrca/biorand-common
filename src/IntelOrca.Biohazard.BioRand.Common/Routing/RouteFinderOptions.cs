using System;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public class RouteFinderOptions
    {
        public int? DebugDepthLimit;
        public Action<object>? DebugDeadendCallback;
    }
}
