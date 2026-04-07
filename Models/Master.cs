using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace CMS.Models
{
    [BsonIgnoreExtraElements]
    public class Master
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public List<string> States { get; set; } = new List<string>();
        public List<string> Cities { get; set; } = new List<string>();
        public List<string> Areas { get; set; } = new List<string>();
        public List<SectorMapping> Sectors { get; set; } = new List<SectorMapping>();
        public List<DepartmentMaster> Departments { get; set; } = new List<DepartmentMaster>();
        public List<DivisionMaster> Divisions { get; set; } = new List<DivisionMaster>();
        public List<SubDivisionMaster> SubDivisions { get; set; } = new List<SubDivisionMaster>();
    }

    public class SectorMapping
    {
        public string State { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string SectorName { get; set; } = string.Empty;
    }

    [BsonIgnoreExtraElements]
    public class DepartmentMaster
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? HeadName { get; set; }
        public int SLADays { get; set; } = 7;
        public string Icon { get; set; } = "💡";
        public string? Email { get; set; }
        public string Status { get; set; } = "Active";
    }

    [BsonIgnoreExtraElements]
    public class DivisionMaster
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
    }

    [BsonIgnoreExtraElements]
    public class SubDivisionMaster
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string DivisionName { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
    }

    public class DepartmentBrief
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
