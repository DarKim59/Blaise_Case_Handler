using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Configuration;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text;
using StatNeth.Blaise.API.DataLink;

namespace BlaiseCaseHandler_UnitTests
{
    [TestClass]
    public class BCH_Tests
    {
        [TestMethod]
        public void Test_SetupRabbit()
        {
            var bch = new BlaiseCaseHandler.BlaiseCaseHandler();
            bch.SetupRabbit();
            Assert.AreNotEqual(null, bch.channel);
        }

        [TestMethod]
        public void Test_ConsumeMessage()
        {
            var bch = new BlaiseCaseHandler.BlaiseCaseHandler();
            bch.SetupRabbit();
            bch.ConsumeMessage();
            Assert.AreNotEqual(null, bch.channel);
        }

        [TestMethod]
        public void Test_GetDataLink_ValidParameters()
        {
            var bch_dl = BlaiseCaseHandler.BlaiseCaseHandler.GetDataLink("DESKTOP-0OF1LSJ", "OPN1901A", "LocalDevelopment");
            Assert.AreNotEqual(null, bch_dl);
        }

        [TestMethod]
        public void Test_GetDataLink_InvalidServerPark()
        {
            var bch_dl = BlaiseCaseHandler.BlaiseCaseHandler.GetDataLink("DESKTOP-0OF1LSJ", "OPN1901A", "");
            Assert.AreEqual(null, bch_dl);
        }

        [TestMethod]
        public void Test_GetDataLink_InvalidHostName()
        {
            var bch_dl = BlaiseCaseHandler.BlaiseCaseHandler.GetDataLink("", "OPN1901A", "LocalDevelopment");
            Assert.AreEqual(null, bch_dl);
        }
    }
} 
