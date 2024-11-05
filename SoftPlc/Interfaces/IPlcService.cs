﻿using System.Collections.Generic;
using SoftPlc.Models;

namespace SoftPlc.Interfaces
{
	public interface IPlcService
	{
		IEnumerable<DatablockDescription> GetDatablocksInfo();
		DatablockDescription GetDatablock(int id);
		void AddDatablock(int id, int size);
		void UpdateDatablockData(int id, byte[] data);
		void RemoveDatablock(int id);
		void SaveDatablocks();
        void UpdateDatablockValue(int id, int index, string type, object value, int? bitPosition);
    }
}