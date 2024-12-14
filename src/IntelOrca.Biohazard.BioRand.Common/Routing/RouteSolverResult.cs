using System;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    [Flags]
    public enum RouteSolverResult
    {
        Ok = 0,
        NodesRemaining = 1 << 0,
        PotentialSoftlock = 1 << 1,
    }
}
