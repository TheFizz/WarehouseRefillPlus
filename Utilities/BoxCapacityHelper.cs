namespace WarehouseRefillPlus.Utilities
{
    public static class BoxCapacityHelper
    {
        public static int GetSlotCapacity(ProductSO productSO)
        {
            if (productSO == null || productSO.GridLayoutInBox == null)
            {
                return 0;
            }

            string boxSize = productSO.GridLayoutInBox.boxSize.ToString();
            switch (boxSize)
            {
                case "_30x20x20":
                case "_40x26x26":
                case "_20x20x20":
                    return 1;
                case "_8x8x8_Bakery":
                case "_8x8x8":
                    return 18;
                case "_15x15x15_IceCreamFlavour":
                case "_15x15x15":
                case "_20x20x10":
                    return 2;
                case "_20x10x7":
                case "_20x10x7_Bakery":
                    return 6;
                case "_22x22x8":
                    return 4;
                default:
                    return 2;
            }
        }
    }
}

