﻿// <copyright file="AclTests.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace ZenLib.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ZenLib;
    using ZenLib.Tests.Model;
    using static ZenLib.Language;
    using static ZenLib.Tests.TestHelper;

    /// <summary>
    /// Tests for Zen working with classes.
    /// </summary>
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class AclTests
    {
        private Acl ExampleAcl()
        {
            var aclLine1 = new AclLine { DstIpLow = 10, DstIpHigh = 20, SrcIpLow = 7, SrcIpHigh = 39, Permitted = true };
            var aclLine2 = new AclLine { DstIpLow = 0, DstIpHigh = 100, SrcIpLow = 0, SrcIpHigh = 100, Permitted = false };
            var lines = new AclLine[2] { aclLine1, aclLine2 };
            return new Acl { Lines = lines };
        }

        private Acl ExampleAcl2()
        {
            var random = new Random(7);
            var lines = new List<AclLine>();

            for (int i = 0; i < 10; i++)
            {
                var dlow = (uint)random.Next();
                var dhigh = (uint)random.Next((int)dlow, int.MaxValue);
                var slow = (uint)random.Next();
                var shigh = (uint)random.Next((int)slow, int.MaxValue);
                var perm = random.Next() % 2 == 0;

                var line = new AclLine
                {
                    DstIpLow = dlow,
                    DstIpHigh = dhigh,
                    SrcIpLow = slow,
                    SrcIpHigh = shigh,
                    Permitted = perm,
                };

                lines.Add(line);
            }

            return new Acl { Lines = lines.ToArray() };
        }

        /// <summary>
        /// Test an acl evaluates correctly.
        /// </summary>
        [TestMethod]
        public void TestAclEvaluate()
        {
            var function = Function<Packet, bool>(p => ExampleAcl().Match(p));
            var result = function.Evaluate(new Packet { DstIp = 12, SrcIp = 8 });
            Assert.IsTrue(result);
        }

        /// <summary>
        /// Check agreement for example acl.
        /// </summary>
        [TestMethod]
        public void TestAclVerify()
        {
            CheckAgreement<Packet>(p => ExampleAcl().Match(p));
        }

        /// <summary>
        /// Test acl with provenance evaluates correctly.
        /// </summary>
        [TestMethod]
        public void TestAclWithLinesEvaluate()
        {
            var function = Function<Packet, Tuple<bool, ushort>>(p => ExampleAcl().MatchProvenance(p));
            var result = function.Evaluate(new Packet { DstIp = 12, SrcIp = 6 });
            Assert.AreEqual(result.Item1, false);
            Assert.AreEqual(result.Item2, (ushort)2);
            var packet = function.Find((p, l) => l.Item2() == 3);
            Assert.AreEqual(3, function.Evaluate(packet.Value).Item2);
        }

        /// <summary>
        /// Check agreement for acl with provenance.
        /// </summary>
        [TestMethod]
        public void TestAclWithLinesVerify()
        {
            CheckAgreement<Packet>(p => ExampleAcl().MatchProvenance(p).Item2() == 2);
        }

        /// <summary>
        /// benchmark.
        /// </summary>
        [TestMethod]
        public void TestAclEvaluatePerformance()
        {
            var acl = ExampleAcl2();

            var function = Function<Packet, bool>(p => acl.Match(p));
            function.Compile();

            var packet = new Packet { DstIp = 200, SrcIp = 0 };
            var watch = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < 10000000; i++)
            {
                function.Evaluate(packet);
            }

            Console.WriteLine($"compiled took: {watch.ElapsedMilliseconds}");
            watch.Restart();

            for (int i = 0; i < 10000000; i++)
            {
                acl.MatchLoop(packet);
            }

            Console.WriteLine($"manual took: {watch.ElapsedMilliseconds}");
            watch.Restart();
        }

        /// <summary>
        /// Test unrolling a transition function.
        /// </summary>
        [TestMethod]
        public void TestUnroll()
        {
            var function = Function<LocatedPacket, LocatedPacket>(lp => StepMany(lp, 3));

            var input = function.Find((inputLp, outputLp) =>
                And(inputLp.GetNode() == 0,
                    outputLp.GetNode() == 2,
                    outputLp.GetPacket().GetDstIp() == 4));

            Assert.IsTrue(input.HasValue);
        }

        private Zen<Option<LocatedPacket>> StepOnce(Zen<LocatedPacket> lp)
        {
            var location = lp.GetNode();
            var packet = lp.GetPacket();
            return If(location == 0,
                    Some(LocatedPacketHelper.Create(1, packet)),
                    If(location == 1,
                        Some(LocatedPacketHelper.Create(2, packet)),
                        Null<LocatedPacket>()));
        }

        private Zen<LocatedPacket> StepMany(Zen<LocatedPacket> initial, int k)
        {
            if (k == 0)
            {
                return initial;
            }

            var newLp = StepOnce(initial);
            return If(newLp.HasValue(), StepMany(newLp.Value(), k - 1), initial);
        }
    }
}
