using System;

namespace IntelOrca.Biohazard.BioRand
{
    public class RandomizerUserException(string reason) : Exception(reason)
    {
    }
}
