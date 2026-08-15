using System;
using System.Collections.Generic;

namespace Test.Shared
{
    /// <summary>
    /// Lightweight assertion helper for Touchstone test cases.
    /// A test case fails by throwing; each method throws <see cref="CheckException"/>
    /// with a descriptive message when the condition is not satisfied.
    /// This keeps Test.Shared free of any test-framework dependency so the exact
    /// same assertions run under the console runner, xUnit, and NUnit.
    /// </summary>
    public static class Check
    {
        /// <summary>
        /// Assert that a condition is true.
        /// </summary>
        public static void IsTrue(bool condition, string message)
        {
            if (!condition) throw new CheckException(message);
        }

        /// <summary>
        /// Assert that a condition is false.
        /// </summary>
        public static void IsFalse(bool condition, string message)
        {
            if (condition) throw new CheckException(message);
        }

        /// <summary>
        /// Assert that a value is not null.
        /// </summary>
        public static void IsNotNull(object value, string message)
        {
            if (value == null) throw new CheckException(message + " (expected non-null)");
        }

        /// <summary>
        /// Assert that a value is null.
        /// </summary>
        public static void IsNull(object value, string message)
        {
            if (value != null) throw new CheckException(message + " (expected null, got '" + value + "')");
        }

        /// <summary>
        /// Assert equality using the default equality comparer.
        /// </summary>
        public static void AreEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new CheckException(message + " (expected '" + expected + "', got '" + actual + "')");
        }

        /// <summary>
        /// Assert inequality using the default equality comparer.
        /// </summary>
        public static void AreNotEqual<T>(T notExpected, T actual, string message)
        {
            if (EqualityComparer<T>.Default.Equals(notExpected, actual))
                throw new CheckException(message + " (did not expect '" + notExpected + "')");
        }

        /// <summary>
        /// Assert that a string is not null or empty.
        /// </summary>
        public static void IsNotEmpty(string value, string message)
        {
            if (string.IsNullOrEmpty(value)) throw new CheckException(message + " (expected non-empty string)");
        }

        /// <summary>
        /// Assert that a double value is within the inclusive range [min, max].
        /// </summary>
        public static void InRange(double value, double min, double max, string message)
        {
            if (value < min || value > max)
                throw new CheckException(message + " (expected [" + min + "," + max + "], got '" + value + "')");
        }

        /// <summary>
        /// Assert that an action throws an exception of type <typeparamref name="TException"/>
        /// (or a derived type).
        /// </summary>
        public static void Throws<TException>(Action action, string message) where TException : Exception
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }
            catch (Exception ex)
            {
                throw new CheckException(message + " (expected " + typeof(TException).Name +
                    ", got " + ex.GetType().Name + ": " + ex.Message + ")");
            }

            throw new CheckException(message + " (expected " + typeof(TException).Name + ", but no exception was thrown)");
        }

        /// <summary>
        /// Assert that an action does not throw any exception.
        /// </summary>
        public static void DoesNotThrow(Action action, string message)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            try
            {
                action();
            }
            catch (Exception ex)
            {
                throw new CheckException(message + " (expected no exception, got " +
                    ex.GetType().Name + ": " + ex.Message + ")");
            }
        }
    }

    /// <summary>
    /// Exception thrown when a <see cref="Check"/> assertion fails.
    /// </summary>
    public sealed class CheckException : Exception
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="message">Failure message.</param>
        public CheckException(string message) : base(message)
        {
        }
    }
}
