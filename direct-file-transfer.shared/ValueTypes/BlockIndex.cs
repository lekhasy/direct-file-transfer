using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace direct_file_transfer.shared.ValueTypes
{
    public class PartIndex
    {
        public PartIndex(int index)
        {
            if(index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index must be non-negative.");
            }
            Value = index;
        }

        public int Value { get; }
    }

    public record PartOffset
    {
        public PartOffset(PartIndex index, PartSize size)
        {
            Value = index.Value * (long)size.Value;
        }
        public long Value { get; }
    }

    public record PartSize
    {
        public PartSize(int size)
        {
            Value = size;
        }
        public int Value { get; }
    }
}
