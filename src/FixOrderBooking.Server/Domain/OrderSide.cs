namespace FixOrderBooking.Server.Domain
{
    /// <summary>
    /// A Buy/Sell FIX Wrapper to facilitate business logic
    /// </summary>
    public class OrderSide
    {
        private char _value;
        public char Value
        {
            get => _value;
            set
            {
                if (!IsValid(value))
                    throw new InvalidOperationException("Invalid OrderSide value");

                _value = value;
            }
        }

        public OrderSide(char val)
        {
            Value = val;
        }

        public override string ToString()
        {
            return Value == Buy ? "Buy" : "Sell";
        }

        public static implicit operator OrderSide(char val) => new(val);
        public static implicit operator char(OrderSide val) => val.Value;
        public static OrderSide Buy => QuickFix.Fields.Side.BUY;
        public static OrderSide Sell => QuickFix.Fields.Side.SELL;
        public static bool IsValid(char val) =>
            val == QuickFix.Fields.Side.BUY || val == QuickFix.Fields.Side.SELL;
    }
}
