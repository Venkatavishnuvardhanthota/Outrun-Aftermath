using System;
using Xunit;
using OutrunAftermath.Simulation.AntiCheat;

namespace OutrunAftermath.Simulation.Tests
{
    public class AntiCheatTests
    {
        [Fact]
        public void MovementValidator_AllowsLegalDisplacement()
        {
            var from = new Vec3(0, 0, 0);
            var to = new Vec3(5, 0, 0); // 5 meters in 1 second at sprint speed (8.5 m/s)
            bool legal = MovementValidator.IsLegalDisplacement(from, to, SpeedLimits.MaxSprintSpeed, 1.0f);
            Assert.True(legal);
        }

        [Fact]
        public void MovementValidator_RejectsImpossibleTeleportation()
        {
            var from = new Vec3(0, 0, 0);
            var to = new Vec3(100, 0, 0); // 100 meters in 0.5s -> impossible speed hack
            bool legal = MovementValidator.IsLegalDisplacement(from, to, SpeedLimits.MaxSprintSpeed, 0.5f);
            Assert.False(legal);
        }

        [Fact]
        public void AimBotDetector_DetectsInstantaneousSnap()
        {
            var from = new Vec3(0, 0, 1); // Looking North
            var to = new Vec3(1, 0, 0);   // Looking East (90 deg turn)
            float dt = 0.010f; // 10 milliseconds -> 9000 deg/sec!

            bool isSnap = AimBotDetector.IsSuspiciousSnap(from, to, dt, out float velocity);
            Assert.True(isSnap);
            Assert.True(velocity > AimBotDetector.MaxHumanAngularVelocityDegPerSec);
        }

        [Fact]
        public void AimBotDetector_AllowsNormalSmoothHumanFlick()
        {
            var from = new Vec3(0, 0, 1);
            var to = new Vec3(0.5f, 0, 0.866f); // 30 deg turn
            float dt = 0.100f; // 100 milliseconds -> 300 deg/sec

            bool isSnap = AimBotDetector.IsSuspiciousSnap(from, to, dt, out float velocity);
            Assert.False(isSnap);
            Assert.True(velocity < AimBotDetector.MaxHumanAngularVelocityDegPerSec);
        }

        [Fact]
        public void PacketSequenceTracker_AcceptsOrderedPackets()
        {
            var tracker = new PacketSequenceTracker(64);
            Assert.True(tracker.ValidateAndAccept(1));
            Assert.True(tracker.ValidateAndAccept(2));
            Assert.True(tracker.ValidateAndAccept(3));
            Assert.True(tracker.ValidateAndAccept(4));
        }

        [Fact]
        public void PacketSequenceTracker_RejectsReplayedPackets()
        {
            var tracker = new PacketSequenceTracker(64);
            Assert.True(tracker.ValidateAndAccept(10));
            Assert.True(tracker.ValidateAndAccept(11));
            // Replay sequence 10
            Assert.False(tracker.ValidateAndAccept(10));
        }

        [Fact]
        public void PacketSequenceTracker_RejectsExpiredPackets()
        {
            var tracker = new PacketSequenceTracker(32);
            Assert.True(tracker.ValidateAndAccept(100));
            // Packet older than window of 32
            Assert.False(tracker.ValidateAndAccept(50));
        }

        [Fact]
        public void MatchResultSigner_ComputesAndVerifiesSignature()
        {
            string matchId = "match_test_99";
            string winner = "Rogues";
            int kills = 14;
            int duration = 340;
            string key = "SecretAuditKey";

            string sig = MatchResultSigner.ComputeSignature(matchId, winner, kills, duration, key);
            Assert.False(string.IsNullOrEmpty(sig));

            bool valid = MatchResultSigner.VerifySignature(matchId, winner, kills, duration, sig, key);
            Assert.True(valid);
        }

        [Fact]
        public void MatchResultSigner_RejectsTamperedResult()
        {
            string matchId = "match_test_99";
            string winner = "Rogues";
            int kills = 14;
            int duration = 340;
            string key = "SecretAuditKey";

            string sig = MatchResultSigner.ComputeSignature(matchId, winner, kills, duration, key);

            // Tamper kill count to 999
            bool valid = MatchResultSigner.VerifySignature(matchId, winner, 999, duration, sig, key);
            Assert.False(valid);
        }
    }
}
