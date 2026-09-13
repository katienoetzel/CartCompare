namespace CartCompare.Services.Models;

public enum MembershipAddResult
{
    Added,
    AlreadyExists,
    RetailerNotFound,
    RetailerDoesNotSupportMembership
}