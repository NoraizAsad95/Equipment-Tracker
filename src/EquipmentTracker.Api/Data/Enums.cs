namespace EquipmentTracker.Api.Data;

public enum TrackingMode { Individual, Quantity }

public enum PersonRole { Staff, Office, Owner }

public enum EquipmentCondition { OK, Damaged }

public enum LineStatus { PendingApproval, Out, Returned, Rejected }

public enum HistoryEventType { CheckedOut, ApprovalRequested, Approved, Rejected, Returned, Restored, Corrected }
