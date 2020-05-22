using System;
using System.Collections.Generic;

namespace WindowsFormsAppSerialPort
{
    internal abstract class Message
    {
        public Message(string commandID)
        {
            this.commandID = commandID;
        }

        public string commandID { get; set; }

        public abstract string Call();
    }

    internal class PingMessage : Message
    {
        public PingMessage(string commandID) : base(commandID)
        {
        }

        public override string Call()
        {
            //return "{Id : " + "C-" + commandID + ", Type : PING, Message : PONG";
            return $"{{\"Id\":\"C-{ commandID }\",\"Type\":\"PING\",\"Message\":\"PONG\"}}";
        }
    }

    internal class ErrorMessage : Message
    {
        public ErrorMessage(string commandID) : base(commandID)
        {
        }

        public override string Call()
        {
            //return "{Id : C-N/A, Type : ERROR, Message : INVALID COMMAND SENT";
            return $"{{\"Id\":\"C-N/A\",\"Type\":\"ERROR\",\"Message\":\"INVALID COMMAND SENT\"}}";
        }
    }

    internal class GetMessage : Message
    {
        public string targetName;

        public GetMessage(string commandID, string targetName) : base(commandID)
        {
            this.targetName = targetName;
        }

        public override string Call()
        {
            string output = DataSource.GetInstance().GetData(targetName).GetAsString();
            if (output == null)
                //return "{Id : " + "C-" + commandID + ", Type : ERROR, Message : GET command failed " + this.targetName + " does not exist in the data source";
                return $"{{\"Id\":\"C-{ commandID }\",\"Type\":\"GET\",\"Message\":\"GET command failed { this.targetName } does not exist in the data source\"}}";
            //return "{Id : " + "C-" + commandID + ", Type : GET, Message : " + output;
            return $"{{\"Id\":\"C-{ commandID }\",\"Type\":\"GET\",\"Message\":\"{ output }\"}}";
        }
    }

    internal class GetAllMessage : Message
    {
        public GetAllMessage(string commandID) : base(commandID)
        {
        }

        public override string Call()
        {
            List<string> output = new List<string>();
            foreach (DataType datum in DataSource.GetInstance().GetAllData())
                output.Add(datum.GetAsString());

            if (output.Count < 1)
                //return "{Id : " + "C-" + commandID + ", Type : GETALL, Message : GETALL command failed, The data source is empty";
                return $"{{\"Id\":\"C-{ commandID }\",\"Type\":\"GET\",\"Message\":\"GETALL command failed, The data source is empty\"}}";
            //return "{Id : " + "C-" + commandID + ", Type : GETALL, Message : \"" + String.Join(",", output.ToArray() + "\"");
            return $"{{\"Id\":\"C-{ commandID }\",\"Type\":\"GET\",\"Message\":\"{ String.Join(",", output.ToArray() + "\"") }\"}}";
        }
    }

    internal class SetMessage : Message
    {
        public string targetName;
        public DataType newValue;

        public SetMessage(string commandID, string targetName, string setValue, string dataType) : base(commandID)
        {
            this.targetName = targetName;

            switch (dataType)
            {
                case "int":
                    newValue = new IntegerDataType(Int32.Parse(setValue));
                    break;

                case "flt":
                    newValue = new FloatDataType(float.Parse(setValue));
                    break;

                case "str":
                    newValue = new StringDataType(setValue);
                    break;

                default:
                    newValue = new EmptyDataType();
                    break;
            }
        }

        public override string Call()
        {
            DataSource.GetInstance().SetData(targetName, newValue);
            //return "{Id : " + "C-" + commandID + ", Type : SET, Message :\"" + targetName + ", " + newValue + "\"";
            return $"{{\"Id\":\"C-{ commandID }\",\"Type\":\"GET\",\"Message\":\"{ targetName },{ newValue }\"}}";
        }
    }
}