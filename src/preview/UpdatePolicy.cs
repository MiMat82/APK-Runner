using System;

namespace APKRunner
{
    internal sealed class UpdatePolicy
    {
        public volatile bool Checking;
        public Version Current;
        public volatile Version Required;
        public bool BlocksNewApps { get { return Checking || (Required != null && Current != null && Required > Current); } }
    }
}
