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
    public class BCC_Tests
    {
        [TestMethod]
        public void Test_Rabbit_Connection()
        {
            var b = new BlaiseCaseHandler.BlaiseCaseHandler();

            // Gets the list of server parks deployed to the target Blaise server.
            List<string> serverParkNames = BlaiseCaseHandler.BlaiseCaseHandler.GetServerParks();

            b.queueNames = new List<string>();

            foreach (string sp in serverParkNames)
            {
                b.queueNames.Add(sp + "." + ConfigurationManager.AppSettings["RabbitRoutingKey"]);
            }

            PublishRabbitMessage(b.queueNames);

            b.SetupRabbitConnection();

            Assert.AreNotEqual(null, b.channel);
        }

        [TestMethod]
        public void Test_ReadQueue()
        {
            // JsonDeserialization seems to not work during testing. this makes testing the use of Rabbit messages difficult.

            var b = new BlaiseCaseHandler.BlaiseCaseHandler();

            // Gets the list of server parks deployed to the target Blaise server.
            List<string> serverParkNames = BlaiseCaseHandler.BlaiseCaseHandler.GetServerParks();

            b.queueNames = new List<string>();

            foreach (string sp in serverParkNames)
            {
                b.queueNames.Add(sp + ConfigurationManager.AppSettings["RabbitQueueSuffix"]);
            }

            b.SetupRabbitConnection();

            b.ReadQueue();

            Assert.AreNotEqual(null, b.channel);
        }

        [TestMethod]
        public void Test_GetDataLink_ValidParameters()
        {
            IDataLink4 dl = null;

            dl = BlaiseCaseHandler.BlaiseCaseHandler.GetDataLink("OPN1901A", "LocalDevelopment");

            Assert.AreNotEqual(null, dl);
        }

        [TestMethod]
        public void Test_GetDataLink_InvalidInstrument()
        {
            IDataLink4 dl = null;

            dl = BlaiseCaseHandler.BlaiseCaseHandler.GetDataLink("FakeInstrument", "LocalDevelopment");

            Assert.AreEqual(null, dl);
        }

        [TestMethod]
        public void Test_GetDataLink_InvalidServerPark()
        {
            IDataLink4 dl = null;

            dl = BlaiseCaseHandler.BlaiseCaseHandler.GetDataLink("OPN1901A", "FakeServerPark");

            Assert.AreEqual(null, dl);
        }

        [TestMethod]
        public void Test_GetServerParks()
        {
            List<string> serverParkList = null;

            serverParkList = BlaiseCaseHandler.BlaiseCaseHandler.GetServerParks();

            Assert.AreNotEqual(0, serverParkList.Count);
        }


        // Tools
        public void PublishRabbitMessage(List<string> queues)
        {
            var factory = new ConnectionFactory()
            {
                HostName = ConfigurationManager.AppSettings["RabbitHostName"],
                UserName = ConfigurationManager.AppSettings["RabbitUserName"],
                Password = ConfigurationManager.AppSettings["RabbitPassword"]
            };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {

                foreach (string queueName in queues)
                {
                    // Create a queue called "Hello" and set its properties"
                    channel.QueueDeclare(queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                    // Write a test message and turn it into bytes
                    string message = "{ \"instrument_name\" : \"HealthSurvey\" }";

                    //"{\"instrument_name\":\"HealthSurvey\",\"server_park\":\"LocalDevelopment\",\"serial_number\":\"999\",\"issue_number\":\"1\",\"interviewer_id\":\"harris\",\"payload\":{ \"health.water\":\"1\", \"health.vegetables\":\"2\"}}";
                    var body = Encoding.UTF8.GetBytes(message);

                    // Publish a message to the rabbit queue
                    channel.BasicPublish(exchange: "",
                        routingKey: queueName,
                        body: body);
                    Console.WriteLine(" [x] Sent {0}", message);
                }
            }
        }
    }
} 
