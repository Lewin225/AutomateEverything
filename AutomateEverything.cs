using System;
using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using AudioPlugSharp;


namespace AutomateEverything
{
    public class AutomateEverything : AudioPluginBase
    {
        enum MessageType
        {
           MESSAGE_VST_LOGGING,
           MESSAGE_NOTE_ON,
           MESSAGE_NOTE_OFF,
           MESSAGE_PARAMETER_VALUE_CHANGE,
           MESSAGE_BPM_CHANGE,
        }

        
        public AutomateEverything()
        {
            Company = "Push Play Media";
            Website = "";
            Contact = "Lewin#0001";
            PluginName = "Automate Everything";
            PluginCategory = "Fx";
            PluginVersion = "1.0.0";

            // Unique 64bit ID for the plugin
            PluginID = 0xF47703946AFC4EF7;
        }

        IPEndPoint inboundPort = new IPEndPoint(IPAddress.Loopback, 6665);
        IPEndPoint outboundPort = new IPEndPoint(IPAddress.Loopback, 25565);

        UdpClient inboundUdpClient = new UdpClient();
        UdpClient outboundUdpClient = new UdpClient();
        
        AudioIOPort monoInput;
        AudioIOPort monoOutput;

        void NetworkInit()
        {

        }
        public override void Initialize()
        {
            base.Initialize();
            Debug.WriteLine("> InitVST");


            for (int i = 0; i < 255; i++)
            {
                AddParameter(new AudioPluginParameter { ID = "Param_"+i, Name = "Parameter "+i, Type = EAudioPluginParameterType.Float, MinValue = 0, MaxValue = 1, DefaultValue = 0, ValueFormat = "", });
            }


            Debug.WriteLine("> Adding parameters done");

            InputPorts = new AudioIOPort[] { monoInput = new AudioIOPort("Mono Input", EAudioChannelConfiguration.Mono) };
            OutputPorts = new AudioIOPort[] { monoOutput = new AudioIOPort("Mono Output", EAudioChannelConfiguration.Mono) };

            outboundUdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            outboundUdpClient.Connect(outboundPort);
            Debug.WriteLine("> Bound outbound port");

            inboundUdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            inboundUdpClient.Client.Bind(inboundPort);
            Debug.WriteLine("> Bound inbound port");
        }

        //public override byte[] SaveState()
        //{
        //    //SaveStateData.ParameterValues.Add(new AudioPluginSaveParameter { ID = "outbound_port", Value = outboundPort.Port });
        //    return base.SaveState();
        //}
        //public override void RestoreState(byte[] stateData)
        //{
        //    base.RestoreState(stateData);
        //    foreach(AudioPluginSaveParameter saveParam in SaveStateData.ParameterValues)
        //    {
        //        if(saveParam.ID == "outbound_port")
        //        {
        //            outboundPort = new IPEndPoint(IPAddress.Loopback, (int)saveParam.Value);
        //            NetworkInit();
        //        }
        //    }

        //}

        public void OnParameterConfigReceived(JsonDocument config)
        {
            //Debug.WriteLine("> Parameter Config Received");
            //var rootElement = config.RootElement;
            //config_version = rootElement.GetProperty("version").GetInt32();
            //var paramConfigs = rootElement.GetProperty("parameters");
            ///ClearParameters();
            ///


            //base.Initialize();

            //foreach ( var param in paramConfigs.EnumerateArray())
            //{
            //    AddParameter(new AudioPluginParameter
            //    {
            //        ID = param.GetProperty("ID").GetString(),
            //        Name = param.GetProperty("Name").GetString(),
            //        Type = (EAudioPluginParameterType)param.GetProperty("Type").GetUInt32(),
            //        MinValue = param.GetProperty("MinValue").GetDouble(),
            //        MaxValue = param.GetProperty("MaxValue").GetDouble(),
            //        DefaultValue = param.GetProperty("DefaultValue").GetDouble(),
            //        ValueFormat = param.GetProperty("ValueFormat").GetString(),
            //    });

            //}

            SendLog(("Parameter change received - " + parameterDict.Count as string) + " parameters active");
        }

        public override void HandleNoteOn(int channel, int noteNumber, float velocity, int sampleOffset)
        {
            //Debug.WriteLine("Note on: " + noteNumber + " offset: " + sampleOffset + " channel:" + channel + " velocity:" + velocity);
            outboundMessage[0] = (byte)channel;
            outboundMessage[1] = (byte)noteNumber;
            outboundMessage[2] = (byte)sampleOffset;
            BitConverter.GetBytes(velocity).CopyTo(outboundMessage, 3);
            Send_outboundMessage(MessageType.MESSAGE_NOTE_ON);
        }

        public override void HandleNoteOff(int channel, int noteNumber, float velocity, int sampleOffset)
        {
            outboundMessage[0] = (byte)channel;
            outboundMessage[1] = (byte)noteNumber;
            outboundMessage[2] = (byte)sampleOffset;
            BitConverter.GetBytes(velocity).CopyTo(outboundMessage, 3);
            Send_outboundMessage(MessageType.MESSAGE_NOTE_OFF);
        }

        public override void HandleParameterChange(AudioPluginParameter parameter, double newNormalizedValue, int sampleOffset)
        {
            base.HandleParameterChange(parameter, newNormalizedValue, sampleOffset);

            outboundMessage[0] = (byte)parameter.ParameterIndex;
            BitConverter.GetBytes(newNormalizedValue).CopyTo(outboundMessage, 1);
            Send_outboundMessage(MessageType.MESSAGE_PARAMETER_VALUE_CHANGE);
        }

        public void SendLog(String Message)
            // Inefficent variable length network sender for debugging only
        {
            var msg = "  " + Message;
            var bmsg = Encoding.UTF8.GetBytes(msg);
            bmsg[0] = (byte)MessageType.MESSAGE_VST_LOGGING;
            outboundUdpClient.Send(bmsg);
            Debug.WriteLine(Encoding.UTF8.GetString(bmsg));
        }



        byte[] OBmessagebuffer = new byte[20];
        byte[] outboundMessage = new byte[18];
       
        void Send_outboundMessage(MessageType messagetype)
        // Fixed length sender for high rate output
        // Set the contents of outboundMessage before calling SendMessage
        {
            Array.Clear(OBmessagebuffer);

            outboundMessage.CopyTo(OBmessagebuffer, 1);
            OBmessagebuffer[0] = (byte)((int)messagetype);

            outboundUdpClient.SendAsync(OBmessagebuffer);

            Array.Clear(outboundMessage);

        }

        public void PrintByteArray(byte[] bytes)
        // Just for debugging
        {
            var sb = new StringBuilder("new byte[] { ");
            foreach (var b in bytes)
            {
                sb.Append(b + ", ");
            }
            sb.Append("}");
            Console.WriteLine(sb.ToString());
        }


        public override void Process()
        {
            base.Process();

            // This will trigger all Midi note events and parameter changes that happend during this process window
            Host.ProcessAllEvents();

            if (inboundUdpClient.Available > 0)
            {
                
                var recvddata = inboundUdpClient.Receive(ref inboundPort);
                if (recvddata != null)
                {
                    OnParameterConfigReceived (JsonDocument.Parse( System.Text.Encoding.ASCII.GetString(recvddata) ));
                }

            }

            //double[] outSamples = monoOutput.GetAudioBuffers()[0];
            //double[] inSamples = monoInput.GetAudioBuffers()[0];
            //outSamples = inSamples; // Pretend to be an effect? - TODO check if this is needed
            // Write out our managed audio data
            //monoOutput.WriteData();
        }
    }
}
