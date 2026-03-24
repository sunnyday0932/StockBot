namespace StockBot.Domain.Enums;

public enum SignalType
{
    Resonance,          // 共振：量價齊揚 + 聲量激增
    BearishDivergence,  // 背離出貨：高聲量 + 股價不漲或反跌
    StealthStrength,    // 潛力抗跌：恐慌聲量 + 股價異常強勢
    SectorRotation      // 族群輪動：二線補漲股偵測
}
