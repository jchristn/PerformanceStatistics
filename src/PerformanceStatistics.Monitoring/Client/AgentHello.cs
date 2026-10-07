using System.Collections.Generic;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// What an agent announces when it connects.
    /// </summary>
    public class AgentHello
    {
        #region Public-Members

        /// <summary>
        /// Host facts.
        /// </summary>
        public HostInfo Info { get; set; } = new HostInfo();

        /// <summary>
        /// Actions this agent's policy allows.
        /// </summary>
        public List<ProcessActionKindEnum> AllowedActions { get; set; } = new List<ProcessActionKindEnum>();

        #endregion
    }
}
