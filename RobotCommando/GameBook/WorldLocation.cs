#nullable enable
using System.Xml.Serialization;

namespace RobotCommando.GameBook;

public enum WorldLocation
{
    [XmlEnum("Unknown")]
    Unknown,

    [XmlEnum("Inherit")]
    Inherit,

    [XmlEnum("Farm")]
    Farm,

    [XmlEnum("Capital City")]
    CapitalCity,

    [XmlEnum("City of Industry")]
    CityOfIndustry,

    [XmlEnum("City of Knowledge")]
    CityOfKnowledge,

    [XmlEnum("City of Pleasure")]
    CityOfPleasure,

    [XmlEnum("City of Storms")]
    CityOfStorms,

    [XmlEnum("City of the Guardians")]
    CityOfTheGuardians,

    [XmlEnum("City of the Jungle")]
    CityOfTheJungle,

    [XmlEnum("City of Worship")]
    CityOfWorship,
}
