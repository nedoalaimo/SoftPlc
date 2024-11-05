using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SoftPlc.Exceptions;
using SoftPlc.Interfaces;
using SoftPlc.Models;

namespace SoftPlc.Controllers
{
    public enum PlcDataType
    {
        [EnumMember(Value = "int")]
        Int = 0,

        [EnumMember(Value = "short")]
        Short = 1,

        [EnumMember(Value = "bool")]
        Bool = 2,

        [EnumMember(Value = "float")]
        Float = 3,

        [EnumMember(Value = "double")]
        Double = 4,

        [EnumMember(Value = "string")]
        String = 5
    }

    /// <inheritdoc />
    [ApiController]
	[Route("api/[controller]")]
	public class DataBlocksController : Controller
    {
	    private readonly IPlcService plcService;

	    /// <inheritdoc />
	    public DataBlocksController(IPlcService plcService)
	    {
		    this.plcService = plcService;
	    }
		/// <summary>
		/// Get the datablocks information of the current soft plc instance
		/// </summary>
		/// <returns></returns>
        // GET api/datablocks
        [HttpGet]
        public IEnumerable<DatablockDescription> Get()
        {
	        return plcService.GetDatablocksInfo();
        }
		/// <summary>
		/// Get the actual datablock configuration
		/// </summary>
		/// <param name="id">The datablock id</param>
		/// <returns></returns>
        // GET api/datablocks/5
        [HttpGet("{id}")]
        public DatablockDescription Get(int id)
        {
	        return plcService.GetDatablock(id);
        }
		/// <summary>
		/// Create a new datablock
		/// </summary>
		/// <param name="id">The datablock id</param>
		/// <param name="size">The datablock size</param>
        // POST api/datablocks
        [HttpPost]
        public void Post(int id, int size)
        {
	        plcService.AddDatablock(id, size);
			plcService.SaveDatablocks();
        }
		/// <summary>
		/// Update the content of a datablock
		/// </summary>
		/// <param name="id">The datablock id</param>
		/// <param name="data">The datablock content in form of array of bytes</param>
		// PUT api/datablocks/5
		[HttpPut("{id}")]
        public void Put(int id, [FromBody]byte[] data)
        {
	        plcService.UpdateDatablockData(id, data);
        }
		/// <summary>
		/// Delete a datablock from the current plc instance
		/// </summary>
		/// <param name="id">The datablock id</param>
        // DELETE api/datablocks/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
	        plcService.RemoveDatablock(id);
        }

        /// <summary>
        /// Update a specific value in a datablock at the given index
        /// </summary>
        /// <param name="id">The datablock id (1-65535)</param>
        /// <param name="index">The starting byte index in the datablock</param>
        /// <param name="type">The type of value to set (supported types: int, short, bool, float, double, string)</param>
        /// <param name="value">The value to set</param>
        /// <remarks>
        /// Sample requests:
        /// 
        /// Set integer value:
        ///
        ///     PUT /api/datablocks/1/value?index=12&amp;type=int
        ///     44
        ///
        /// Set string value:
        ///
        ///     PUT /api/datablocks/2/value?index=30&amp;type=string
        ///     "Hello World"
        ///
        /// Set boolean value:
        ///
        ///     PUT /api/datablocks/3/value?index=0&amp;type=bool
        ///     true
        ///
        /// Set float value:
        ///
        ///     PUT /api/datablocks/4/value?index=8&amp;type=float
        ///     123.45
        /// 
        /// Type sizes in bytes:
        /// - int: 4 bytes
        /// - short: 2 bytes
        /// - bool: 1 byte
        /// - float: 4 bytes
        /// - double: 8 bytes
        /// - string: length + 1 bytes (first byte stores string length)
        /// </remarks>
        /// <response code="200">Value successfully updated</response>
        /// <response code="400">Invalid request (index out of range, value too large, etc.)</response>
        /// <response code="404">Datablock not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPut("{id}/value")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult PutValue(
        [FromRoute] int id,
        [FromQuery] int index,
        [FromQuery] PlcDataType type,
        [FromBody] object value,
        [FromQuery] int? bitPosition)
        {
            try
            {
                plcService.UpdateDatablockValue(id, index, type.ToString(), value, bitPosition);
                return Ok();
            }
            catch (DbNotFoundException)
            {
                return NotFound($"Datablock {id} not found");
            }
            catch (IndexOutOfRangeException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (DateExceedsDbLengthException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception)
            {
                return StatusCode(500, "An unexpected error occurred");
            }
        }
    }
}
