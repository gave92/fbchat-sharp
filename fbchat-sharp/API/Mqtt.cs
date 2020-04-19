using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace fbchat_sharp.API
{
    /// <summary>
    /// Helper, to listen for incoming Facebook events.
    /// </summary>
    public class Listener
    {
        /// Mqtt client for receiving messages
        private IMqttClient mqttClient;
        /// Stores and manages state required for most Facebook requests.
        private Session _session;
        private bool _chat_on;
        private bool _foreground;
        private int _mqtt_sequence_id;
        private string _sync_token;

        /// <summary>
        /// Helper, to listen for incoming Facebook events.
        /// </summary>
        private Listener(Session session, bool chat_on, bool foreground)
        {
            this._mqtt_sequence_id = 0;
            this._sync_token = null;
            this._chat_on = chat_on;
            this._foreground = foreground;
            this._session = session;
        }

        /// <summary>
        /// Initialize a connection to the Facebook MQTT service.
        /// </summary>
        /// <param name="session">The session to use when making requests.</param>
        /// <param name="onEvent">Callback called on event received.</param>
        /// <param name="chat_on"></param>
        /// <param name="foreground"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async static Task<Listener> Connect(Session session, Func<FB_Event, Task> onEvent, bool chat_on = true, bool foreground = true, CancellationToken cancellationToken = default(CancellationToken))
        {
            var listener = new Listener(session, chat_on, foreground);

            var factory = new MqttFactory();
            if (listener.mqttClient != null)
            {
                listener.mqttClient.UseDisconnectedHandler((e) => { });
                try { await listener.mqttClient.DisconnectAsync(); }
                catch { }
                listener.mqttClient.Dispose();
                listener.mqttClient = null;
            }
            listener.mqttClient = factory.CreateMqttClient();

            listener.mqttClient.UseConnectedHandler(async e =>
            {
                Debug.WriteLine("MQTT: connected with server");

                // Subscribe to a topic
                await listener.mqttClient.SubscribeAsync(
                    new TopicFilterBuilder().WithTopic("/legacy_web").Build(),
                    new TopicFilterBuilder().WithTopic("/webrtc").Build(),
                    new TopicFilterBuilder().WithTopic("/br_sr").Build(),
                    new TopicFilterBuilder().WithTopic("/sr_res").Build(),
                    new TopicFilterBuilder().WithTopic("/t_ms").Build(), // Messages
                    new TopicFilterBuilder().WithTopic("/thread_typing").Build(), // Group typing notifications
                    new TopicFilterBuilder().WithTopic("/orca_typing_notifications").Build(), // Private chat typing notifications
                    new TopicFilterBuilder().WithTopic("/thread_typing").Build(),
                    new TopicFilterBuilder().WithTopic("/notify_disconnect").Build(),
                    new TopicFilterBuilder().WithTopic("/orca_presence").Build());

                // I read somewhere that not doing this might add message send limits
                await listener.mqttClient.UnsubscribeAsync("/orca_message_notifications");
                await listener._messenger_queue_publish();

                Debug.WriteLine("MQTT: subscribed");
                if (onEvent != null) await onEvent(new FB_Connect());
            });

            listener.mqttClient.UseApplicationMessageReceivedHandler(async e =>
            {
                Debug.WriteLine("MQTT: received application message");
                Debug.WriteLine($"+ Topic = {e.ApplicationMessage.Topic}");
                Debug.WriteLine($"+ Payload = {Encoding.UTF8.GetString(e.ApplicationMessage.Payload)}");
                Debug.WriteLine($"+ QoS = {e.ApplicationMessage.QualityOfServiceLevel}");

                var event_type = e.ApplicationMessage.Topic;
                var data = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                try
                {
                    var event_data = Utils.to_json(data);
                    await listener._parse_message(event_type, event_data, onEvent);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }
            });

            listener.mqttClient.UseDisconnectedHandler(async e =>
            {
                Debug.WriteLine("MQTT: disconnected from server");
                if (onEvent != null) await onEvent(new FB_Disconnect() { Reason = "Connection lost, retrying" });
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    await listener.mqttClient.ConnectAsync(listener._get_connect_options(), cancellationToken);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }
            });

            await listener.mqttClient.ConnectAsync(listener._get_connect_options(), cancellationToken);

            return listener;
        }

        private async Task<int> _fetch_mqtt_sequence_id()
        {
            // Get the sync sequence ID used for the /messenger_sync_create_queue call later.
            // This is the same request as fetch_thread_list, but with includeSeqID=true
            var j = await this._session.graphql_request(GraphQL.from_doc_id("1349387578499440", new Dictionary<string, object> {
                { "limit", 0 },
                { "tags", new string[] {ThreadLocation.INBOX } },
                { "before", null },
                { "includeDeliveryReceipts", false },
                { "includeSeqID", true },
            }));

            var sequence_id = j.get("viewer")?.get("message_threads")?.get("sync_sequence_id")?.Value<int>();
            if (sequence_id == null)
                throw new FBchatException("Could not fetch sequence id");
            return (int)sequence_id;
        }

        private async Task _messenger_queue_publish()
        {
            this._mqtt_sequence_id = await _fetch_mqtt_sequence_id();

            var payload = new Dictionary<string, object>(){
                        { "sync_api_version", 10 },
                        { "max_deltas_able_to_process", 1000 },
                        { "delta_batch_size", 500 },
                        { "encoding", "JSON" },
                        { "entity_fbid", this._session.user.uid }
                };

            if (this._sync_token == null)
            {
                Debug.WriteLine("MQTT: sending messenger sync create queue request");
                var message = new MqttApplicationMessageBuilder()
                .WithTopic("/messenger_sync_create_queue")
                .WithPayload(JsonConvert.SerializeObject(
                    new Dictionary<string, object>(payload) {
                        { "initial_titan_sequence_id", this._mqtt_sequence_id.ToString() },
                        { "device_params", null }
                    })).Build();
                await mqttClient.PublishAsync(message);
            }
            else
            {
                Debug.WriteLine("MQTT: sending messenger sync get diffs request");
                var message = new MqttApplicationMessageBuilder()
                .WithTopic("/messenger_sync_get_diffs")
                .WithPayload(JsonConvert.SerializeObject(
                    new Dictionary<string, object>(payload) {
                        { "last_seq_id", this._mqtt_sequence_id.ToString() },
                        { "sync_token", this._sync_token }
                    })).Build();
                await mqttClient.PublishAsync(message);
            }
        }

        private IMqttClientOptions _get_connect_options()
        {
            // Random session ID
            var sid = new Random().Next(1, int.MaxValue);

            // The MQTT username. There's no password.
            var username = new Dictionary<string, object>() {
                { "u", this._session.user.uid }, // USER_ID
                { "s", sid },
                { "cp", 3 }, // CAPABILITIES
                { "ecp", 10 }, // ENDPOINT_CAPABILITIES
                { "chat_on", this._chat_on }, // MAKE_USER_AVAILABLE_IN_FOREGROUND
                { "fg", this._foreground }, // INITIAL_FOREGROUND_STATE
                // Not sure if this should be some specific kind of UUID, but it's a random one now.
                { "d", Guid.NewGuid().ToString() },
                { "ct", "websocket" }, // CLIENT_TYPE
                { "mqtt_sid", "" }, // CLIENT_MQTT_SESSION_ID
                // Application ID, taken from facebook.com
                { "aid", 219994525426954 },
                { "st", new string[0] }, // SUBSCRIBE_TOPICS
                { "pm", new string[0] },
                { "dc", "" },
                { "no_auto_fg", true }, // NO_AUTOMATIC_FOREGROUND
                { "gas", null }
            };

            // Headers for the websocket connection. Not including Origin will cause 502's.
            // User agent and Referer also probably required. Cookie is how it auths.
            // Accept is there just for fun.
            var cookies = this._session.get_cookies();

            var headers = new Dictionary<string, string>() {
                { "Referer", "https://www.facebook.com" },
                { "User-Agent", Utils.USER_AGENTS[0] },
                { "Cookie", string.Join(";", cookies[".facebook.com"].Select(c => $"{c.Name}={c.Value}"))},
                { "Accept", "*/*"},
                { "Origin", "https://www.messenger.com" }
            };

            // Use WebSocket connection.
            var options = new MqttClientOptionsBuilder()
                        .WithClientId("mqttwsclient")
                        .WithWebSocketServer($"wss://edge-chat.facebook.com/chat?region=lla&sid={sid}",
                            new MqttClientOptionsBuilderWebSocketParameters() { RequestHeaders = headers })
                        .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V310)
                        .WithCredentials(JsonConvert.SerializeObject(username), "")
                        .Build();
            return options;
        }

        private async Task _parse_message(string topic, JToken data, Func<FB_Event, Task> onEvent)
        {
            try
            {
                if (topic == "/t_ms")
                {
                    if (data.get("errorCode") != null)
                    {
                        Debug.WriteLine(string.Format("MQTT error: {0}", data.get("errorCode")?.Value<string>()));
                        this._sync_token = null;
                        await this._messenger_queue_publish();
                    }
                    else
                    {
                        // Update sync_token when received
                        // This is received in the first message after we've created a messenger
                        // sync queue.
                        if (data?.get("syncToken") != null && data?.get("firstDeltaSeqId") != null)
                        {
                            this._sync_token = data?.get("syncToken")?.Value<string>();
                            this._mqtt_sequence_id = data?.get("firstDeltaSeqId")?.Value<int>() ?? _mqtt_sequence_id;
                        }

                        // Update last sequence id when received
                        if (data?.get("lastIssuedSeqId") != null)
                        {
                            this._mqtt_sequence_id = data?.get("lastIssuedSeqId")?.Value<int>() ?? _mqtt_sequence_id;
                        }
                    }
                }

                foreach (FB_Event ev in EventCommon.parse_events(_session, topic, data))
                {
                    if (onEvent != null) await onEvent(ev);
                }
            }
            catch (FBchatParseError ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        /// <summary>
        /// Cleans up the variables from startListening
        /// </summary>
        public async Task Disconnect()
        {
            // Stop mqtt client
            if (this.mqttClient != null)
            {
                this.mqttClient.UseDisconnectedHandler((e) => { });
                try { await this.mqttClient.DisconnectAsync(); }
                catch { }
                this.mqttClient.Dispose();
                this.mqttClient = null;
            }

            // Cleans up the variables from startListening
            this._sync_token = null;
        }

        /// <summary>
        /// Set the `foreground` value while listening.
        /// </summary>
        /// <param name="value">Whether to show if client is active</param>
        public async Task setForeground(bool value)
        {
            if (this._foreground != value)
            {
                if (this.mqttClient != null && this.mqttClient.IsConnected)
                {
                    var message = new MqttApplicationMessageBuilder()
                        .WithTopic("/foreground_state")
                        .WithPayload(JsonConvert.SerializeObject(
                            new Dictionary<string, object>() {
                        { "make_user_available_when_in_foreground", value }
                    })).Build();
                    await mqttClient.PublishAsync(message);
                    this._foreground = value;
                }
            }
        }

        /// <summary>
        /// Set the `chat_on` value while listening.
        /// </summary>
        /// <param name="value">Whether to show if client is active</param>
        public async Task setChatOn(bool value)
        {
            if (this._chat_on != value)
            {
                if (this.mqttClient != null && this.mqttClient.IsConnected)
                {
                    var message = new MqttApplicationMessageBuilder()
                        .WithTopic("/set_client_settings")
                        .WithPayload(JsonConvert.SerializeObject(
                            new Dictionary<string, object>() {
                        { "make_user_available_when_in_foreground", value }
                    })).Build();
                    await mqttClient.PublishAsync(message);
                    this._chat_on = value;
                }
            }
        }
    }
}
