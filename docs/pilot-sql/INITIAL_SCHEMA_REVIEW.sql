IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [AnalysisRuns] (
        [Id] uniqueidentifier NOT NULL,
        [AtUtc] datetime2 NOT NULL,
        [Trigger] nvarchar(40) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [DevicesEvaluated] int NOT NULL,
        [Findings] int NOT NULL,
        [InitiatedBy] nvarchar(180) NOT NULL,
        CONSTRAINT [PK_AnalysisRuns] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [AnalysisSchedules] (
        [Id] uniqueidentifier NOT NULL,
        [Enabled] bit NOT NULL,
        [EveryMinutes] int NOT NULL,
        [LastRunUtc] datetime2 NULL,
        CONSTRAINT [PK_AnalysisSchedules] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] uniqueidentifier NOT NULL,
        [DisplayName] nvarchar(140) NOT NULL,
        [IsDisabled] bit NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [AuditEvents] (
        [Id] uniqueidentifier NOT NULL,
        [AtUtc] datetime2 NOT NULL,
        [Actor] nvarchar(180) NOT NULL,
        [Action] nvarchar(120) NOT NULL,
        [Target] nvarchar(120) NOT NULL,
        [Outcome] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_AuditEvents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [ClientSessions] (
        [Id] uniqueidentifier NOT NULL,
        [DeviceId] uniqueidentifier NOT NULL,
        [ClientHash] nvarchar(64) NOT NULL,
        [Ssid] nvarchar(80) NULL,
        [Band] nvarchar(20) NULL,
        [FirstSeenUtc] datetime2 NOT NULL,
        [LastSeenUtc] datetime2 NOT NULL,
        [RssiDbm] decimal(6,2) NULL,
        [SnrDb] decimal(6,2) NULL,
        [RetryPct] decimal(6,2) NULL,
        [AssociatedVlan] int NULL,
        [Source] nvarchar(80) NOT NULL,
        CONSTRAINT [PK_ClientSessions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [ClusterMembers] (
        [Id] uniqueidentifier NOT NULL,
        [ConductorDeviceId] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Model] nvarchar(60) NOT NULL,
        [Zone] nvarchar(80) NULL,
        [IpMasked] nvarchar(120) NULL,
        [DiscoveryConfidence] nvarchar(30) NOT NULL,
        [ObservedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ClusterMembers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [ComplianceFindings] (
        [Id] uniqueidentifier NOT NULL,
        [DeviceId] uniqueidentifier NOT NULL,
        [Rule] nvarchar(70) NOT NULL,
        [Evidence] nvarchar(600) NOT NULL,
        [Severity] nvarchar(24) NOT NULL,
        [State] nvarchar(24) NOT NULL,
        [DetectedUtc] datetime2 NOT NULL,
        [ClosedUtc] datetime2 NULL,
        CONSTRAINT [PK_ComplianceFindings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [ConfigExecutionEvents] (
        [Id] uniqueidentifier NOT NULL,
        [ProposalId] uniqueidentifier NOT NULL,
        [AtUtc] datetime2 NOT NULL,
        [Stage] nvarchar(60) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [Detail] nvarchar(800) NOT NULL,
        CONSTRAINT [PK_ConfigExecutionEvents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [ConfigProposals] (
        [Id] uniqueidentifier NOT NULL,
        [DeviceId] uniqueidentifier NOT NULL,
        [Action] nvarchar(48) NOT NULL,
        [PayloadJson] nvarchar(2000) NOT NULL,
        [PayloadSha256] nvarchar(64) NOT NULL,
        [ApprovedPayloadSha256] nvarchar(64) NOT NULL,
        [RequestedBy] nvarchar(150) NOT NULL,
        [ApprovedBy] nvarchar(150) NULL,
        [SecondApprovedBy] nvarchar(150) NULL,
        [Justification] nvarchar(500) NULL,
        [LastOutcome] nvarchar(600) NULL,
        [BaselineFirmware] nvarchar(50) NULL,
        [BaselineClusterMemberCount] int NULL,
        [RequestedUtc] datetime2 NOT NULL,
        [ApprovedUtc] datetime2 NULL,
        [FinishedUtc] datetime2 NULL,
        [State] nvarchar(48) NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_ConfigProposals] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [ConfigurationSnapshots] (
        [Id] uniqueidentifier NOT NULL,
        [DeviceId] uniqueidentifier NOT NULL,
        [Firmware] nvarchar(60) NOT NULL,
        [DataSha256] nvarchar(64) NOT NULL,
        [SanitizedJson] nvarchar(max) NOT NULL,
        [CapturedUtc] datetime2 NOT NULL,
        [CapturedBy] nvarchar(120) NOT NULL,
        CONSTRAINT [PK_ConfigurationSnapshots] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [DeviceCapabilities] (
        [Id] uniqueidentifier NOT NULL,
        [DeviceId] uniqueidentifier NOT NULL,
        [Operation] nvarchar(70) NOT NULL,
        [Firmware] nvarchar(50) NOT NULL,
        [State] nvarchar(48) NOT NULL,
        [EvidenceReference] nvarchar(150) NOT NULL,
        [CheckedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_DeviceCapabilities] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [DeviceGroups] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Notes] nvarchar(100) NULL,
        [LocationId] uniqueidentifier NULL,
        [Kind] nvarchar(30) NOT NULL,
        CONSTRAINT [PK_DeviceGroups] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [DevicePlacements] (
        [Id] uniqueidentifier NOT NULL,
        [DeviceId] uniqueidentifier NOT NULL,
        [LocationId] uniqueidentifier NOT NULL,
        [FromUtc] datetime2 NOT NULL,
        [ThroughUtc] datetime2 NULL,
        [ChangedBy] nvarchar(180) NOT NULL,
        CONSTRAINT [PK_DevicePlacements] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [Devices] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Kind] nvarchar(48) NOT NULL,
        [ManagementIp] nvarchar(100) NOT NULL,
        [SecretRef] nvarchar(80) NOT NULL,
        [LocationId] uniqueidentifier NULL,
        [Firmware] nvarchar(60) NULL,
        [ClusterName] nvarchar(60) NULL,
        [LastStatus] nvarchar(30) NOT NULL,
        [LastIssue] nvarchar(300) NULL,
        [LastProbeUtc] datetime2 NULL,
        [LastSeenUtc] datetime2 NULL,
        [Enabled] bit NOT NULL,
        CONSTRAINT [PK_Devices] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [GroupMemberships] (
        [Id] uniqueidentifier NOT NULL,
        [GroupId] uniqueidentifier NOT NULL,
        [DeviceId] uniqueidentifier NOT NULL,
        [AssignedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_GroupMemberships] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [HardwareAcceptanceRuns] (
        [Id] uniqueidentifier NOT NULL,
        [DeviceId] uniqueidentifier NOT NULL,
        [StartedUtc] datetime2 NOT NULL,
        [CompletedUtc] datetime2 NOT NULL,
        [Actor] nvarchar(180) NOT NULL,
        [Verdict] nvarchar(50) NOT NULL,
        [Summary] nvarchar(200) NULL,
        [SqlServerVerified] bit NOT NULL,
        [DeviceApiAuthenticated] bit NOT NULL,
        [FreshSnapshotPersisted] bit NOT NULL,
        [ExpectedInventoryPersisted] bit NOT NULL,
        [ReportedApCount] int NULL,
        [StoredClusterMembers] int NOT NULL,
        [StoredPortSamples] int NOT NULL,
        [Firmware] nvarchar(60) NULL,
        [EvidenceJson] nvarchar(1500) NOT NULL,
        CONSTRAINT [PK_HardwareAcceptanceRuns] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [IntegrationTargets] (
        [Id] uniqueidentifier NOT NULL,
        [Kind] nvarchar(80) NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Url] nvarchar(200) NOT NULL,
        [SecretRef] nvarchar(80) NOT NULL,
        [Enabled] bit NOT NULL,
        CONSTRAINT [PK_IntegrationTargets] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [Locations] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Type] nvarchar(40) NOT NULL,
        [ParentId] uniqueidentifier NULL,
        [Address] nvarchar(200) NULL,
        CONSTRAINT [PK_Locations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [NetworkAlerts] (
        [Id] uniqueidentifier NOT NULL,
        [DeviceId] uniqueidentifier NULL,
        [Fingerprint] nvarchar(60) NOT NULL,
        [Severity] nvarchar(20) NOT NULL,
        [Category] nvarchar(120) NOT NULL,
        [Detail] nvarchar(500) NOT NULL,
        [State] nvarchar(30) NOT NULL,
        [FirstSeenUtc] datetime2 NOT NULL,
        [LastSeenUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_NetworkAlerts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [OperationsIncidents] (
        [Id] uniqueidentifier NOT NULL,
        [DeviceId] uniqueidentifier NULL,
        [LocationId] uniqueidentifier NULL,
        [Title] nvarchar(120) NOT NULL,
        [Description] nvarchar(1200) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [Severity] nvarchar(20) NOT NULL,
        [Owner] nvarchar(140) NULL,
        [OpenedUtc] datetime2 NOT NULL,
        [ClosedUtc] datetime2 NULL,
        CONSTRAINT [PK_OperationsIncidents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [PhysicalSpaces] (
        [Id] uniqueidentifier NOT NULL,
        [LocationId] uniqueidentifier NOT NULL,
        [ParentSpaceId] uniqueidentifier NULL,
        [Name] nvarchar(120) NOT NULL,
        [Type] nvarchar(20) NOT NULL,
        CONSTRAINT [PK_PhysicalSpaces] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [PolicyTemplates] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Scope] nvarchar(50) NOT NULL,
        [LocationId] uniqueidentifier NULL,
        [NonSecretJson] nvarchar(max) NOT NULL,
        [Version] int NOT NULL,
        [CreatedBy] nvarchar(140) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PolicyTemplates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [PortObservations] (
        [Id] uniqueidentifier NOT NULL,
        [DeviceId] uniqueidentifier NOT NULL,
        [Port] nvarchar(90) NOT NULL,
        [AtUtc] datetime2 NOT NULL,
        [RxBytes] bigint NULL,
        [TxBytes] bigint NULL,
        [RxErrors] bigint NULL,
        [TxErrors] bigint NULL,
        [UtilizationPct] decimal(6,2) NULL,
        [PoeWatts] decimal(7,2) NULL,
        [LinkState] nvarchar(30) NOT NULL,
        [Source] nvarchar(80) NOT NULL,
        CONSTRAINT [PK_PortObservations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [RadioObservations] (
        [Id] uniqueidentifier NOT NULL,
        [DeviceId] uniqueidentifier NOT NULL,
        [RadioName] nvarchar(120) NULL,
        [AtUtc] datetime2 NOT NULL,
        [Channel] int NULL,
        [WidthMhz] int NULL,
        [PowerDbm] decimal(6,2) NULL,
        [NoiseFloorDbm] decimal(6,2) NULL,
        [UtilizationPct] decimal(6,2) NULL,
        [ErrorPct] decimal(6,2) NULL,
        [Source] nvarchar(120) NOT NULL,
        [Quality] nvarchar(30) NOT NULL,
        CONSTRAINT [PK_RadioObservations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [Recommendations] (
        [Id] uniqueidentifier NOT NULL,
        [DeviceId] uniqueidentifier NOT NULL,
        [Fingerprint] nvarchar(100) NOT NULL,
        [Title] nvarchar(120) NOT NULL,
        [Evidence] nvarchar(800) NOT NULL,
        [ProposedSetting] nvarchar(300) NOT NULL,
        [Risk] nvarchar(20) NOT NULL,
        [State] nvarchar(48) NOT NULL,
        [AtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(180) NOT NULL,
        [ReviewedBy] nvarchar(180) NULL,
        [ReviewedUtc] datetime2 NULL,
        [Executed] bit NOT NULL,
        CONSTRAINT [PK_Recommendations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [ReportDefinitions] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Kind] nvarchar(50) NOT NULL,
        [LocationId] uniqueidentifier NULL,
        [Enabled] bit NOT NULL,
        [CreatedBy] nvarchar(120) NOT NULL,
        CONSTRAINT [PK_ReportDefinitions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [SecurityObservations] (
        [Id] uniqueidentifier NOT NULL,
        [DeviceId] uniqueidentifier NULL,
        [ObservedUtc] datetime2 NOT NULL,
        [EventType] nvarchar(80) NOT NULL,
        [Severity] nvarchar(30) NOT NULL,
        [Description] nvarchar(1200) NOT NULL,
        [Source] nvarchar(80) NOT NULL,
        CONSTRAINT [PK_SecurityObservations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [ServicePathProbes] (
        [Id] uniqueidentifier NOT NULL,
        [DeviceId] uniqueidentifier NULL,
        [LocationId] uniqueidentifier NULL,
        [AtUtc] datetime2 NOT NULL,
        [Type] nvarchar(80) NOT NULL,
        [Target] nvarchar(200) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [Detail] nvarchar(350) NOT NULL,
        [TestAgent] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_ServicePathProbes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [ShadowPredictions] (
        [Id] uniqueidentifier NOT NULL,
        [DeviceId] uniqueidentifier NOT NULL,
        [AtUtc] datetime2 NOT NULL,
        [ModelVersion] nvarchar(60) NOT NULL,
        [CongestionRisk] decimal(6,3) NULL,
        [Reason] nvarchar(700) NOT NULL,
        [ShadowOnly] bit NOT NULL,
        CONSTRAINT [PK_ShadowPredictions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [SitePermissions] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [LocationId] uniqueidentifier NOT NULL,
        [Role] nvarchar(50) NOT NULL,
        [GrantedUtc] datetime2 NOT NULL,
        [GrantedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_SitePermissions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [TelemetryRollups] (
        [Id] uniqueidentifier NOT NULL,
        [DeviceId] uniqueidentifier NOT NULL,
        [BucketUtc] datetime2 NOT NULL,
        [BucketMinutes] int NOT NULL,
        [SampleCount] int NOT NULL,
        [AverageUtilizationPct] decimal(6,2) NULL,
        [MaxUtilizationPct] decimal(6,2) NULL,
        [AverageNoiseFloorDbm] decimal(6,2) NULL,
        CONSTRAINT [PK_TelemetryRollups] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [TelemetrySnapshots] (
        [Id] uniqueidentifier NOT NULL,
        [DeviceId] uniqueidentifier NOT NULL,
        [AtUtc] datetime2 NOT NULL,
        [ApiConnected] bit NOT NULL,
        [ApCount] int NULL,
        [ClientCount] int NULL,
        [VlanCount] int NULL,
        [PortCount] int NULL,
        [RadioChannel] int NULL,
        [ChannelWidthMhz] int NULL,
        [ChannelUtilizationPct] decimal(6,2) NULL,
        [NoiseFloorDbm] decimal(6,2) NULL,
        [ClientRetryPct] decimal(6,2) NULL,
        [ObservationSource] nvarchar(80) NOT NULL,
        [ArmJson] nvarchar(max) NOT NULL,
        [MetricAvailabilityJson] nvarchar(3000) NOT NULL,
        [Problem] nvarchar(200) NULL,
        CONSTRAINT [PK_TelemetrySnapshots] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [TopologyLinks] (
        [Id] uniqueidentifier NOT NULL,
        [DeviceId] uniqueidentifier NOT NULL,
        [LocalPort] nvarchar(90) NOT NULL,
        [NeighborDeviceId] uniqueidentifier NULL,
        [NeighborName] nvarchar(120) NULL,
        [NeighborPort] nvarchar(90) NULL,
        [Evidence] nvarchar(40) NOT NULL,
        [ObservedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_TopologyLinks] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_AuditEvents_AtUtc] ON [AuditEvents] ([AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_ClientSessions_ClientHash] ON [ClientSessions] ([ClientHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_ClientSessions_DeviceId_LastSeenUtc] ON [ClientSessions] ([DeviceId], [LastSeenUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_ClientSessions_LastSeenUtc] ON [ClientSessions] ([LastSeenUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ClusterMembers_ConductorDeviceId_Name] ON [ClusterMembers] ([ConductorDeviceId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_ComplianceFindings_DeviceId_Rule_State] ON [ComplianceFindings] ([DeviceId], [Rule], [State]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_ConfigExecutionEvents_ProposalId_AtUtc] ON [ConfigExecutionEvents] ([ProposalId], [AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_ConfigProposals_DeviceId_State] ON [ConfigProposals] ([DeviceId], [State]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_ConfigurationSnapshots_DeviceId_CapturedUtc] ON [ConfigurationSnapshots] ([DeviceId], [CapturedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DeviceCapabilities_DeviceId_Operation] ON [DeviceCapabilities] ([DeviceId], [Operation]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DeviceGroups_Name] ON [DeviceGroups] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_DevicePlacements_DeviceId_ThroughUtc] ON [DevicePlacements] ([DeviceId], [ThroughUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Devices_ManagementIp] ON [Devices] ([ManagementIp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_Devices_SecretRef] ON [Devices] ([SecretRef]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GroupMemberships_GroupId_DeviceId] ON [GroupMemberships] ([GroupId], [DeviceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_HardwareAcceptanceRuns_DeviceId_CompletedUtc] ON [HardwareAcceptanceRuns] ([DeviceId], [CompletedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Locations_ParentId_Name] ON [Locations] ([ParentId], [Name]) WHERE [ParentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_NetworkAlerts_Fingerprint] ON [NetworkAlerts] ([Fingerprint]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PhysicalSpaces_LocationId_ParentSpaceId_Name] ON [PhysicalSpaces] ([LocationId], [ParentSpaceId], [Name]) WHERE [ParentSpaceId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_PortObservations_AtUtc] ON [PortObservations] ([AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_PortObservations_DeviceId_Port_AtUtc] ON [PortObservations] ([DeviceId], [Port], [AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_RadioObservations_AtUtc] ON [RadioObservations] ([AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_RadioObservations_DeviceId_AtUtc] ON [RadioObservations] ([DeviceId], [AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_Recommendations_Fingerprint] ON [Recommendations] ([Fingerprint]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_SecurityObservations_ObservedUtc] ON [SecurityObservations] ([ObservedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_ServicePathProbes_AtUtc] ON [ServicePathProbes] ([AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_ShadowPredictions_DeviceId_AtUtc] ON [ShadowPredictions] ([DeviceId], [AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SitePermissions_UserId_LocationId_Role] ON [SitePermissions] ([UserId], [LocationId], [Role]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TelemetryRollups_DeviceId_BucketUtc_BucketMinutes] ON [TelemetryRollups] ([DeviceId], [BucketUtc], [BucketMinutes]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_TelemetrySnapshots_AtUtc] ON [TelemetrySnapshots] ([AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE INDEX [IX_TelemetrySnapshots_DeviceId_AtUtc] ON [TelemetrySnapshots] ([DeviceId], [AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TopologyLinks_DeviceId_LocalPort] ON [TopologyLinks] ([DeviceId], [LocalPort]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105145943_InitialProduction'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261105145943_InitialProduction', N'10.0.0');
END;

COMMIT;
GO

