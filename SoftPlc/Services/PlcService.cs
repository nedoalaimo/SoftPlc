using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SoftPlc.Interfaces;
using SoftPlc.Models;
using Microsoft.Extensions.Configuration;
using System.IO;
using Newtonsoft.Json;
using SoftPlc.Exceptions;

namespace SoftPlc.Services
{
	public class PlcService : IPlcService, IDisposable
	{
		private readonly S7Server server;
		private readonly bool serverRunning;
		private readonly string datablockFilename = "datablocks.json";
		private readonly ConcurrentDictionary<int, DatablockDescription> datablocks = new ConcurrentDictionary<int, DatablockDescription>();
		public PlcService(IConfiguration configuration)
		{
			Console.WriteLine("Initializing plc service...");

			server = new S7Server();

			var usedPlcPort = 102;

			if(configuration.GetChildren().Any(item => item.Key.Equals("plcPort")))
			{	
				UInt16 plcPort;
				var parsed = UInt16.TryParse(configuration["plcPort"], out plcPort);
				if(parsed)
					server.SetParam(S7Consts.p_u16_LocalPort, ref plcPort);
				usedPlcPort = plcPort;
			}

			var error = server.Start();
			serverRunning = error == 0;
			if (serverRunning) Console.WriteLine($"plc server started on port {usedPlcPort}!");
			else Console.WriteLine($"plc server error {error}");
            ReadDataBlocks();
		}

		private void CheckServerRunning()
		{
			if(!serverRunning) throw new Exception("Plc server is not running");
		}

		public void SaveDatablocks()
		{
			var settingsFile = Path.Combine(GetSaveLocation(), datablockFilename);
            var json = JsonConvert.SerializeObject(datablocks, Formatting.Indented);
			File.WriteAllText(settingsFile, json);
		}

        private void ReadDataBlocks()
        {
            var settingsFile = Path.Combine(GetSaveLocation(), "datablocks.json");

			try
			{
				if(File.Exists(settingsFile))
				{
                    var json = File.ReadAllText(Path.Combine(GetSaveLocation(),settingsFile));
                	var retrievedDatablock = JsonConvert.DeserializeObject<Dictionary<int, DatablockDescription>>(json);
					foreach(var item in retrievedDatablock)
						AddDatablock(item.Key, item.Value);
				}
			}
			catch(Exception e)
			{
                Console.WriteLine($"Error while deserializing data blocks {e.Message}");
			}

        }

		private string GetSaveLocation()
		{
            try
            {
	            var dataPath = Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.DataPath);
				if(!string.IsNullOrEmpty(dataPath))
					return dataPath;
            }
			catch(Exception e)
			{
                Console.WriteLine($"Error during retrieving env variable DATA_PATH {e.Message}");
			}
			var location = System.Reflection.Assembly.GetExecutingAssembly().Location;
			return Path.GetDirectoryName(location);
		}

		private void ReleaseUnmanagedResourcesAndSaveDatablocks()
		{
			Console.WriteLine("Stopping plc server...");
			server.Stop();
            SaveDatablocks();
		}

		public void Dispose()
		{
			ReleaseUnmanagedResourcesAndSaveDatablocks();
			GC.SuppressFinalize(this);
		}

		~PlcService()
		{
			ReleaseUnmanagedResourcesAndSaveDatablocks();
		}

		public IEnumerable<DatablockDescription> GetDatablocksInfo()
		{
			CheckServerRunning();
			return datablocks.Select(pair => pair.Value);
		}

		public DatablockDescription GetDatablock(int id)
		{
			CheckServerRunning();
            DbOutOfRangeException.ThrowIfInvalid(id);

            if (datablocks.TryGetValue(id, out var db)) 
                return db;
			else
			    throw new DbNotFoundException(id);
		}

		private void AddDatablock(int id, DatablockDescription datablock)
		{
			AddDatablock(id, datablock.Size);
			UpdateDatablockData(id, datablock.Data);
		}

		public void AddDatablock(int id, int size)
		{
			CheckServerRunning();
            DbOutOfRangeException.ThrowIfInvalid(id);
            InvalidDbSizeException.ThrowIfInvalid(size);

			var db = new DatablockDescription(id, size);
			if (!datablocks.TryAdd(id, db))
                throw new DbExistsException(id);

			server.RegisterArea(S7Server.srvAreaDB, id, ref datablocks[id].Data, datablocks[id].Data.Length);
		}

		public void UpdateDatablockData(int id, byte[] data)
		{
            DbOutOfRangeException.ThrowIfInvalid(id);
            
            if (!datablocks.TryGetValue(id, out var db))
                throw new DbNotFoundException(id);

			if (data.Length > db.Data.Length) 
                throw new DateExceedsDbLengthException(id, db.Data.Length, data.Length);

			Array.Copy(data, datablocks[id].Data, data.Length);
		}

		public void RemoveDatablock(int id)
		{
            DbOutOfRangeException.ThrowIfInvalid(id);

            if (datablocks.TryRemove(id, out _))
                server.UnregisterArea(S7Server.srvAreaDB, id);
            else
                throw new DbNotFoundException(id);
        }

        public void UpdateDatablockValue(int id, int index, string type, object value, int? bitPosition = null)
        {
            DbOutOfRangeException.ThrowIfInvalid(id);
            if (!datablocks.TryGetValue(id, out var db))
                throw new DbNotFoundException(id);
            if (index < 0 || index >= db.Data.Length)
                throw new IndexOutOfRangeException($"Index {index} is out of range for datablock {id} with length {db.Data.Length}");

            byte[] convertedBytes;
            switch (type.ToLower())
            {
                case "int":
                    // S7 INT: 16-bit signed integer
                    if (index + 2 > db.Data.Length)
                        throw new DateExceedsDbLengthException(id, db.Data.Length, index + 2);
                    short intValue = Convert.ToInt16(value);
                    convertedBytes = BitConverter.GetBytes(intValue);
                    Array.Reverse(convertedBytes);
                    Array.Copy(convertedBytes, 0, db.Data, index, 2);
                    break;

                case "dint":
                    // S7 DINT: 32-bit signed integer
                    if (index + 4 > db.Data.Length)
                        throw new DateExceedsDbLengthException(id, db.Data.Length, index + 4);
                    int dintValue = Convert.ToInt32(value);
                    convertedBytes = BitConverter.GetBytes(dintValue);
                    Array.Reverse(convertedBytes);
                    Array.Copy(convertedBytes, 0, db.Data, index, 4);
                    break;

                case "word":
                    // S7 WORD: 16-bit unsigned integer
                    if (index + 2 > db.Data.Length)
                        throw new DateExceedsDbLengthException(id, db.Data.Length, index + 2);
                    ushort wordValue = Convert.ToUInt16(value);
                    convertedBytes = BitConverter.GetBytes(wordValue);
                    Array.Reverse(convertedBytes);
                    Array.Copy(convertedBytes, 0, db.Data, index, 2);
                    break;

                case "dword":
                    // S7 DWORD: 32-bit unsigned integer
                    if (index + 4 > db.Data.Length)
                        throw new DateExceedsDbLengthException(id, db.Data.Length, index + 4);
                    uint dwordValue = Convert.ToUInt32(value);
                    convertedBytes = BitConverter.GetBytes(dwordValue);
                    Array.Reverse(convertedBytes);
                    Array.Copy(convertedBytes, 0, db.Data, index, 4);
                    break;

                case "byte":
                    // S7 BYTE: 8-bit unsigned integer
                    if (index + 1 > db.Data.Length)
                        throw new DateExceedsDbLengthException(id, db.Data.Length, index + 1);
                    db.Data[index] = Convert.ToByte(value);
                    break;

                case "bool":
                case "boolean":
                    int bitPos = bitPosition ?? 0;
                    if (bitPos < 0 || bitPos > 7)
                        throw new ArgumentException("Bit position must be between 0 and 7");

                    if (index >= db.Data.Length)
                        throw new DateExceedsDbLengthException(id, db.Data.Length, index + 1);

                    bool boolValue = Convert.ToBoolean(value);

                    // S7 bit ordering is right to left (0 to 7)
                    if (boolValue)
                        // Set bit at position - right to left ordering (0 to 7)
                        db.Data[index] |= (byte)(1 << bitPos);
                    else
                        // Clear bit at position - right to left ordering (0 to 7)
                        db.Data[index] &= (byte)~(1 << bitPos);
                    break;

                case "real":
                    // S7 REAL: 32-bit floating point
                    if (index + 4 > db.Data.Length)
                        throw new DateExceedsDbLengthException(id, db.Data.Length, index + 4);
                    float realValue = Convert.ToSingle(value);
                    convertedBytes = BitConverter.GetBytes(realValue);
                    Array.Reverse(convertedBytes);
                    Array.Copy(convertedBytes, 0, db.Data, index, 4);
                    break;

                case "string":
                    // S7 STRING: Maximum length byte + current length byte + character bytes
                    string stringValue = value.ToString();

                    // S7 string header
                    const int S7_STRING_HEADER_SIZE = 2;  // Max length byte + current length byte
                    byte maxLength = db.Data[index];      // Read the max length from DB

                    // Get the actual string bytes (S7 strings are ASCII)
                    byte[] stringBytes = System.Text.Encoding.ASCII.GetBytes(stringValue);
                    if (stringBytes.Length > maxLength)
                        stringBytes = stringBytes.Take(maxLength).ToArray();

                    // Verify space
                    if (index + S7_STRING_HEADER_SIZE + maxLength > db.Data.Length)
                        throw new DateExceedsDbLengthException(id, db.Data.Length, index + S7_STRING_HEADER_SIZE + maxLength);

                    // Current length (don't modify max length as it's predefined)
                    db.Data[index + 1] = (byte)stringBytes.Length;

                    // Write the string data
                    Array.Copy(stringBytes, 0, db.Data, index + S7_STRING_HEADER_SIZE, stringBytes.Length);

                    // Fill remaining bytes with zeros
                    Array.Fill(db.Data, (byte)0, index + S7_STRING_HEADER_SIZE + stringBytes.Length,
                              maxLength - stringBytes.Length);
                    break;

                case "time":
                    // S7 TIME: 32-bit time in milliseconds
                    if (index + 4 > db.Data.Length)
                        throw new DateExceedsDbLengthException(id, db.Data.Length, index + 4);
                    int timeValue = Convert.ToInt32(value); // Milliseconds
                    convertedBytes = BitConverter.GetBytes(timeValue);
                    Array.Reverse(convertedBytes);
                    Array.Copy(convertedBytes, 0, db.Data, index, 4);
                    break;

                case "s5time":
                    // S7 S5TIME: 16-bit time
                    if (index + 2 > db.Data.Length)
                        throw new DateExceedsDbLengthException(id, db.Data.Length, index + 2);
                    ushort s5timeValue = Convert.ToUInt16(value);
                    convertedBytes = BitConverter.GetBytes(s5timeValue);
                    Array.Reverse(convertedBytes);
                    Array.Copy(convertedBytes, 0, db.Data, index, 2);
                    break;

                default:
                    throw new ArgumentException($"Unsupported type: {type}");
            }
        }
    }
}