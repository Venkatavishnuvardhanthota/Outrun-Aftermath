using System;
using System.Security.Cryptography;
using System.Text;
using OutrunAftermath.Simulation.Combat;

namespace OutrunAftermath.Simulation.AntiCheat
{
    /// <summary>
    /// Strict movement speed limits and teleportation bounds for server-side validation.
    /// </summary>
    public static class SpeedLimits
    {
        public const float MaxCrouchSpeed = 3.0f; // m/s
        public const float MaxWalkSpeed = 5.5f;   // m/s
        public const float MaxSprintSpeed = 8.5f;  // m/s
        public const float MaxSlideSpeed = 11.0f;  // m/s
        public const float MaxVehicleSpeed = 40.0f; // m/s
        public const float AbsoluteMaxDisplacementPerSec = 14.0f; // on-foot absolute cap including latency burst
    }

    /// <summary>
    /// Telemetry detector for instantaneous aim snapping (aimbot / lock-on detection).
    /// Analyzes rotational angular velocity and directional deltas.
    /// </summary>
    public static class AimBotDetector
    {
        public const float MaxHumanAngularVelocityDegPerSec = 1800f; // extreme flick-shot threshold

        public static float CalculateAngularVelocity(Vec3 fromDir, Vec3 toDir, float dtSeconds)
        {
            if (dtSeconds <= 0.0001f) return 0f;

            // Normalize vectors
            float lenFrom = MathF.Sqrt(fromDir.X * fromDir.X + fromDir.Y * fromDir.Y + fromDir.Z * fromDir.Z);
            float lenTo = MathF.Sqrt(toDir.X * toDir.X + toDir.Y * toDir.Y + toDir.Z * toDir.Z);
            if (lenFrom < 0.0001f || lenTo < 0.0001f) return 0f;

            float dot = (fromDir.X * toDir.X + fromDir.Y * toDir.Y + fromDir.Z * toDir.Z) / (lenFrom * lenTo);
            dot = Math.Clamp(dot, -1f, 1f);

            float angleRad = MathF.Acos(dot);
            float angleDeg = angleRad * (180f / MathF.PI);

            return angleDeg / dtSeconds;
        }

        public static bool IsSuspiciousSnap(Vec3 fromDir, Vec3 toDir, float dtSeconds, out float velocityDegPerSec)
        {
            velocityDegPerSec = CalculateAngularVelocity(fromDir, toDir, dtSeconds);
            // If the rotation is faster than human threshold in a single sub-frame (< 20ms)
            return dtSeconds < 0.025f && velocityDegPerSec > MaxHumanAngularVelocityDegPerSec;
        }
    }

    /// <summary>
    /// Monotonic packet sequence validator to detect and reject replay attacks or duplicate commands.
    /// </summary>
    public sealed class PacketSequenceTracker
    {
        private uint _highestSeenSequence;
        private readonly int _windowSize;
        private readonly bool[] _window;

        public PacketSequenceTracker(int windowSize = 64)
        {
            _windowSize = Math.Max(16, windowSize);
            _window = new bool[_windowSize];
            _highestSeenSequence = 0;
        }

        public bool ValidateAndAccept(uint sequenceId)
        {
            if (sequenceId == 0) return true;

            // Ahead of highest sequence
            if (sequenceId > _highestSeenSequence)
            {
                uint gap = sequenceId - _highestSeenSequence;
                if (gap < (uint)_windowSize)
                {
                    // Shift window
                    for (int i = _windowSize - 1; i >= (int)gap; i--)
                    {
                        _window[i] = _window[i - (int)gap];
                    }
                    for (int i = 0; i < Math.Min((int)gap, _windowSize); i++)
                    {
                        _window[i] = false;
                    }
                }
                else
                {
                    Array.Clear(_window, 0, _windowSize);
                }

                _highestSeenSequence = sequenceId;
                _window[0] = true;
                return true;
            }

            // Older packet within window
            uint diff = _highestSeenSequence - sequenceId;
            if (diff >= (uint)_windowSize)
            {
                // Expired / too old
                return false;
            }

            int index = (int)diff;
            if (_window[index])
            {
                // Duplicate / replayed packet!
                return false;
            }

            _window[index] = true;
            return true;
        }
    }

    /// <summary>
    /// Anti-tamper cryptographic signing for match completion payloads.
    /// Prevents client result manipulation, forged kills, or illegal XP rewards.
    /// </summary>
    public static class MatchResultSigner
    {
        public static string ComputeSignature(string matchId, string winnerTeam, int totalKills, int totalDurationSeconds, string secretKey)
        {
            string payload = $"{matchId}:{winnerTeam}:{totalKills}:{totalDurationSeconds}";
            byte[] keyBytes = Encoding.UTF8.GetBytes(secretKey ?? "OutrunAftermath_DefaultSimulationSecret_2026");
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

            using var hmac = new HMACSHA256(keyBytes);
            byte[] hash = hmac.ComputeHash(payloadBytes);
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }
            return sb.ToString();
        }

        public static bool VerifySignature(string matchId, string winnerTeam, int totalKills, int totalDurationSeconds, string signature, string secretKey)
        {
            if (string.IsNullOrWhiteSpace(signature)) return false;

            string expected = ComputeSignature(matchId, winnerTeam, totalKills, totalDurationSeconds, secretKey);
            byte[] a = Encoding.UTF8.GetBytes(expected);
            byte[] b = Encoding.UTF8.GetBytes(signature.ToLowerInvariant());
            if (a.Length != b.Length) return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }
    }
}
