using System.ServiceProcess;
using System.Text;
using System.IO;
using System.Security;
using StatNeth.Blaise.API.ServerManager;
using RabbitMQ.Client;
using System.Web.Script.Serialization;
using RabbitMQ.Client.Events;
using System.Configuration;
using System;
using StatNeth.Blaise.API.DataLink;
using StatNeth.Blaise.API.Meta;
using DataRecordAPI = StatNeth.Blaise.API.DataRecord;
using System.Collections.Generic;
using log4net;
using log4net.Config;

namespace BlaiseCaseHandler
{

    /// <summary>
    /// Class to hold the data received from the RabbitMQ messaging service.
    /// </summary>
    public class MessageData
    {
        public string serial_number { get; set; }
        public string case_id { get; set; }
        public string source_hostname { get; set; }
        public string source_server_park { get; set; }
        public string source_instrument { get; set; }
        public string dest_hostname { get; set; }
        public string dest_server_park { get; set; }
        public string dest_instrument { get; set; }
        public string action { get; set; }
    }

    public partial class BlaiseCaseHandler : ServiceBase
    {
        // Instantiate logger.
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        // Objects for connecting and setting up RabbitMQ.
        public IConnection connection;
        public IModel channel;
        public EventingBasicConsumer consumer;

        /// <summary>
        /// Class constructor for initialising the service.
        /// </summary>        
        public BlaiseCaseHandler()
        {
            InitializeComponent();
        }

        /// <summary>
        /// This method is our entry point when debugging. It allows us to use the service without running the installation steps.
        /// </summary>
        public void OnDebug()
        {
            OnStart(null);
        }

        /// <summary>
        /// OnStart method triggers when a service starts.
        /// </summary>
        /// <param name="args">Optional argument that can be passed on service start.</param>
        protected override void OnStart(string[] args)
        {
            log.Info("Blaise Case Handler service started.");

            // Connect to RabbitMQ and setup channels.
            while (!SetupRabbit())
            {
                // Keep re-trying RabbitMQ connection until connected.
                log.Info("Waiting for RabbitMQ connection...");
                System.Threading.Thread.Sleep(5000);
            }

            // Consume and process messages on the RabbitMQ queue.
            ConsumeMessage();
        }

        /// <summary>
        /// OnStop method triggers when the service stops.
        /// </summary>
        protected override void OnStop()
        {
            log.Info("Blaise Case Handler service stopped.");
        }

        /// <summary>
        /// Method for connecting to RabbitMQ and setting up the channels.
        /// </summary>
        public bool SetupRabbit()
        {
            log.Info("Setting up RabbitMQ.");

            try
            {

                // Create a connection to RabbitMQ using the Rabbit credentials stored in the app.config file.
                var connFactory = new ConnectionFactory()
                {
                    HostName = ConfigurationManager.AppSettings["RabbitHostName"],
                    UserName = ConfigurationManager.AppSettings["RabbitUserName"],
                    Password = ConfigurationManager.AppSettings["RabbitPassword"]
                };
                connection = connFactory.CreateConnection();
                channel = connection.CreateModel();

                // Get the exchange and queue details from the app.config file.
                string exchangeName = ConfigurationManager.AppSettings["RabbitExchange"];
                string queueName = ConfigurationManager.AppSettings["HandlerQueueName"];

                // Declare the exchange for receiving messages.
                channel.ExchangeDeclare(exchange: exchangeName, type: "direct", durable: true);
                log.Info("Exchange declared - " + exchangeName);

                // Declare the queue for receiving messages.
                channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
                log.Info("Queue declared - " + queueName);

                // Bind the queue for receiving messages.
                channel.QueueBind(queue: queueName, exchange: exchangeName, routingKey: queueName);
                log.Info("Queue binding complete - Queue: " + queueName + " / Exchange: " + exchangeName + " / Routing Key: " + queueName);

                // Declare the queue for sending status updates.
                string caseStatusQueueName = ConfigurationManager.AppSettings["CaseStatusQueueName"];
                channel.QueueDeclare(queue: caseStatusQueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
                log.Info("Queue declared - " + caseStatusQueueName);

                // Only consuming one message at a time.
                channel.BasicQos(0, 1, false);

                // Create the consumer object which will run our code when receiving messages.
                consumer = new EventingBasicConsumer(channel);
                log.Info("Consumer object created.");

                log.Info("RabbitMQ setup complete.");

                return true;
            }
            catch
            {
                log.Info("Unable to establish RabbitMQ connection.");
                return false;
            }
        }

        /// <summary>
        /// Method for consuming and processing messages on the RabbitMQ queue.
        /// </summary>
        public void ConsumeMessage()
        {
            // Objects for working with Blaise data sets.
            IDataLink4 dl_source = null;
            IDatamodel dm_source = null;
            IDataLink4 dl_dest = null;
            IDatamodel dm_dest = null;

            // Functionality to be performed when a message is received.
            consumer.Received += (model, ea) =>
            {
                // Extract the message and encode it.
                var body = ea.Body;
                var message = Encoding.UTF8.GetString(body);
                log.Info("Message received - " + message);

                MessageData data = null;
                try
                {
                    // Take the serialized JSON string and deserialize it into a MessageData object.
                    data = new JavaScriptSerializer().Deserialize<MessageData>(message);

                    // Connect to the Blaise source data set.
                    dl_source = GetDataLink(data.source_hostname, data.source_instrument, data.source_server_park);
                    dm_source = dl_source.Datamodel;

                    // Connect to the Blaise destination data set.
                    dl_dest = GetDataLink(data.dest_hostname, data.dest_instrument, data.dest_server_park);
                    dm_dest = dl_dest.Datamodel;

                    // Identify the primary key in the source data set.
                    var key = DataRecordAPI.DataRecordManager.GetKey(dm_source, "PRIMARY");

                    // Assign the primary key the value of the 'serial_number' recieved from the RabbitMQ message.
                    key.Fields[0].DataValue.Assign(data.serial_number);

                    // Check if a case with this key exists in the source data set.
                    if (dl_source.KeyExists(key))
                    {
                        // Read in the case.
                        var case_record = dl_source.ReadRecord(key);

                        // Copy or move the case from the source to destination based on the 'action' received from the message.
                        switch (data.action)
                        {
                            // Copy action received.
                            case "copy":
                                dl_dest.Write(case_record);
                                if (!dl_dest.KeyExists(key))
                                {
                                    SendStatus(MakeStatusJson(data, "Error"));
                                    log.Error(data.dest_instrument + " case " + data.serial_number + " NOT copied from " + data.source_server_park + "@" + data.source_hostname + " to " + data.dest_server_park + "@" + data.dest_hostname + ".");
                                }
                                if (dl_dest.KeyExists(key))
                                {
                                    SendStatus(MakeStatusJson(data, "Case Copied"));
                                    log.Info(data.dest_instrument + " case " + data.serial_number + " copied from " + data.source_server_park + "@" + data.source_hostname + " to " + data.dest_server_park + "@" + data.dest_hostname + ".");
                                }                                
                                break;
                            // Move action received.
                            case "move":
                                dl_dest.Write(case_record);
                                dl_source.Delete(key);
                                if (!dl_dest.KeyExists(key))
                                {
                                    SendStatus(MakeStatusJson(data, "Error"));
                                    log.Error(data.dest_instrument + " case " + data.serial_number + " NOT moved from " + data.source_server_park + "@" + data.source_hostname + " to " + data.dest_server_park + "@" + data.dest_hostname + ".");
                                }
                                if ((dl_dest.KeyExists(key)) && (dl_source.KeyExists(key)))
                                {
                                    SendStatus(MakeStatusJson(data, "Warn"));
                                    log.Warn(data.dest_instrument + " case " + data.serial_number + " copied from " + data.source_server_park + "@" + data.source_hostname + " but also still exists in " + data.dest_server_park + "@" + data.dest_hostname + ".");
                                }
                                if ((dl_dest.KeyExists(key)) && (!dl_source.KeyExists(key)))
                                {
                                    SendStatus(MakeStatusJson(data, "Case Moved"));
                                    log.Info(data.dest_instrument + " case " + data.serial_number + " moved from " + data.source_server_park + "@" + data.source_hostname + " to " + data.dest_server_park + "@" + data.dest_hostname + ".");
                                }
                                break;
                            // Invalid action received.
                            default:
                                SendStatus(MakeStatusJson(data, "Invalid Action"));
                                log.Error("Invalid action requested - " + data.action);
                                break;
                        }
                    }
                    else
                    {
                        SendStatus(MakeStatusJson(data, "Case NOT Found"));
                        log.Error("Case " + data.serial_number.ToString() + " doesn't exist in source database.");                        
                    }
                }
                catch (Exception e)
                {
                    SendStatus(MakeStatusJson(data, "Error"));
                    log.Error(e);                    
                }
                // Remove from queue when done processing
                channel.BasicAck(ea.DeliveryTag, false);
            };

            // Consume and process any messages already held on the queue.
            string queueName = ConfigurationManager.AppSettings["HandlerQueueName"];
            channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
        }

        /// <summary>
        /// Method for connecting to Blaise data sets.
        /// </summary>
        /// /// <param name="hostname">The name of the hostname.</param>
        /// <param name="instrumentName">The name of the instrument.</param>
        /// <param name="serverPark">The name of the server park.</param>
        /// <returns> IDataLink4 object for the connected server park.</returns>
        public static IDataLink4 GetDataLink(string hostname, string instrumentName, string serverPark)
        {
            // Get authenication details from the App.config file.
            // For now we assume all Blaise servers will have the same authenication details.
            string userName = ConfigurationManager.AppSettings["BlaiseServerUserName"];
            string password = ConfigurationManager.AppSettings["BlaiseServerPassword"];
            int port = 8031;

            // Overwrite authenication details when testing locally.
            if (hostname == ConfigurationManager.AppSettings["BlaiseServerHostNameLocal"])
            {
                userName = ConfigurationManager.AppSettings["BlaiseServerUserNameLocal"];
                password = ConfigurationManager.AppSettings["BlaiseServerPasswordLocal"];
            }

            // Get the GIID of the instrument.
            Guid instrumentID = Guid.NewGuid();
            try
            {
                // Connect to the Blaise Server Manager.
                IConnectedServer serManConn = ServerManager.ConnectToServer(hostname, port, userName, GetPassword(password));

                // Loop through the surveys installed on the server to find the GUID of the survey we are working on.
                bool foundSurvey = false;
                foreach (ISurvey survey in serManConn.GetServerPark(serverPark).Surveys)
                {
                    if (survey.Name == instrumentName)
                    {
                        instrumentID = survey.InstrumentID;
                        foundSurvey = true;
                    }
                }
                if (foundSurvey == false)
                {
                    log.Error("Survey " + instrumentName + " not found on " + serverPark + "@" + hostname + ".");
                }

                // Connect to the data.
                IRemoteDataServer dataLinkConn = DataLinkManager.GetRemoteDataServer(hostname, 8033, userName, GetPassword(password));

                return dataLinkConn.GetDataLink(instrumentID, serverPark);
            }
            catch (Exception e)
            {
                log.Error(e.Message);
                return null;
            }
        }

        /// <summary>
        /// Converts a password to secure string.
        /// </summary>
        /// <param name="pw">Password to be converted to secure string.</param>
        /// <returns></returns>
        private static SecureString GetPassword(string pw)
        {
            char[] passwordChars = pw.ToCharArray();
            SecureString password = new SecureString();
            foreach (char c in passwordChars)
            {
                password.AppendChar(c);
            }
            return password;
        }

        /// <summary>
        /// Builds a JSON status object to be used in the SendStatus method.
        /// </summary>
        /// <param name="data"> Case object containing case information.</param>
        /// <param name="status"> Status of the case being processed.</param>
        /// <returns>Json object containing required information.</returns>
        private Dictionary<string,
        string> MakeStatusJson(MessageData data, string status)
        {
            Dictionary<string, string> jsonData = new Dictionary<string, string>();

            jsonData["service_name"] = ConfigurationManager.AppSettings["ServiceName"];
            jsonData["serial_number"] = data.serial_number;
            jsonData["case_id"] = data.case_id;
            jsonData["source_hostname"] = data.source_hostname;
            jsonData["source_server_park"] = data.source_server_park;
            jsonData["source_instrument"] = data.source_instrument;
            jsonData["dest_hostname"] = data.dest_hostname;
            jsonData["dest_server_park"] = data.dest_server_park;
            jsonData["dest_instrument"] = data.dest_instrument;
            jsonData["action"] = data.action;
            jsonData["status"] = status;            

            return jsonData;
        }

        /// <summary>
        /// Sends a status message to RabbitMQ.
        /// </summary>
        private void SendStatus(Dictionary<string, string> jsonData)
        {
            string message = new JavaScriptSerializer().Serialize(jsonData);
            var body = Encoding.UTF8.GetBytes(message);
            string caseStatusQueueName = ConfigurationManager.AppSettings["CaseStatusQueueName"];
            channel.BasicPublish(exchange: "", routingKey: caseStatusQueueName, body: body);
            log.Info("Message sent to RabbitMQ " + caseStatusQueueName + " queue - " + message);
        }
    }
}
