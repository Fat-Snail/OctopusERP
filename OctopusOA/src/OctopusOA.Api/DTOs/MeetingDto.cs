namespace OctopusOA.Api.DTOs;

public class MeetingRoomDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string? Location { get; set; }
    public List<string> Equipment { get; set; } = new();
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int Status { get; set; } = 1;
    public DateTime CreateTime { get; set; }
}

public class CreateMeetingRoomRequest
{
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string? Location { get; set; }
    public List<string> Equipment { get; set; } = new();
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int Status { get; set; } = 1;
}

public class UpdateMeetingRoomRequest : CreateMeetingRoomRequest
{
    public long Id { get; set; }
}

public class MeetingBookingDto
{
    public long Id { get; set; }
    public long RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public long UmcUserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Description { get; set; }
    public List<long> Attendees { get; set; } = new();
    public List<string> AttendeeNames { get; set; } = new();
    public string Status { get; set; } = "confirmed";
    public DateTime CreateTime { get; set; }
}

public class CreateBookingRequest
{
    public long RoomId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Description { get; set; }
    public List<long> Attendees { get; set; } = new();
}
