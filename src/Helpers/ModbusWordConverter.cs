namespace MyConsoleApp.Helpers;

public static class ModbusWordConverter
{
    public static float ToSingle(ushort[] registers)
    {
        if (registers == null || registers.Length < 2)
        {
            throw new ArgumentException(
                "At least two Modbus registers are required to decode a float value.",
                nameof(registers));
        }

        uint combined = ((uint)registers[0] << 16) | registers[1];
        return BitConverter.Int32BitsToSingle(unchecked((int)combined));
    }
}
