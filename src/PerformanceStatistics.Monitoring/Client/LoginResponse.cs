using System;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// A session token.
    /// </summary>
    public class LoginResponse
    {
        #region Public-Members

        /// <summary>
        /// Bearer token.
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Expiry (UTC).
        /// </summary>
        public DateTime ExpiresUtc { get; set; }

        /// <summary>
        /// User name.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// True if the user may request process actions.
        /// </summary>
        public bool IsAdmin { get; set; } = false;

        #endregion
    }
}
