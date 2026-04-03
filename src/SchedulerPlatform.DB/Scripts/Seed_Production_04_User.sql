-- ============================================================================
-- Seed Production: User and UserPermission Tables
-- ============================================================================
-- Run this DIRECTLY against the Production database (not UAT).
-- Requires Client with ClientId = 1 to exist (run Client seed first).
--
-- Users are renumbered sequentially (1-12) to eliminate gaps from deleted
-- UAT records. UserPermission UserId references are remapped accordingly.
--
-- UAT UserId -> Production UserId mapping:
--   2  -> 1  (admin / lcassin@cassinfo.com)
--   3  -> 2  (viewer / leecassin@icloud.com)
--   4  -> 3  (editor / lcassin@charter.net)
--   6  -> 4  (jlwilson@cassinfo.com)
--   8  -> 5  (dmiller@cassinfo.com)
--   9  -> 6  (gthomas@cassinfo.com)
--   10 -> 7  (mvogel@cassinfo.com)
--   11 -> 8  (mwillis@cassinfo.com)
--   12 -> 9  (blindstrom@cassinfo.com)
--   13 -> 10 (ashea@cassinfo.com)
--   14 -> 11 (LBoogaard@cassinfo.com)
--   15 -> 12 (TLyday@cassinfo.com)
--
-- All INSERT statements use IF NOT EXISTS checks for idempotent re-runs.
-- ============================================================================

-- ============================================================================
-- STEP 1: Seed Users (with new sequential IDs, no gaps)
-- ============================================================================

SET IDENTITY_INSERT [dbo].[User] ON;
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[User] WHERE [UserId] = 1)
BEGIN
    INSERT INTO [dbo].[User] ([UserId], [Username], [Email], [FirstName], [LastName], [ClientId], [IsActive], [ExternalUserId], [ExternalIssuer], [PasswordHash], [IsSystemAdmin], [LastLoginDateTime], [MustChangePassword], [PasswordChangedDateTime], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted], [PreferredTimeZone])
    VALUES (1, N'admin', N'lcassin@cassinfo.com', N'Default', N'Admin', 1, 1, N'entra|32a589c1-6ddf-47fe-bf57-2318dbee22a0', N'entra', N'AQAAAAIAAYagAAAAEHwNKy51I8oNB+Vy6qIFX9uq8om8SAb06M0vKHPmLUuCYStaqk6ZRA+K3YTwB6EXxQ==', 1, '20260402 21:26:07.878', 0, '20251203 18:35:03.057', '20251113 18:23:44.180', '20260402 21:26:07.878', N'System', N'lcassin@cassinfo.com', 0, N'Eastern Standard Time');
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[User] WHERE [UserId] = 2)
BEGIN
    INSERT INTO [dbo].[User] ([UserId], [Username], [Email], [FirstName], [LastName], [ClientId], [IsActive], [ExternalUserId], [ExternalIssuer], [PasswordHash], [IsSystemAdmin], [LastLoginDateTime], [MustChangePassword], [PasswordChangedDateTime], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted], [PreferredTimeZone])
    VALUES (2, N'viewer', N'leecassin@icloud.com', N'View', N'Only', 1, 0, NULL, NULL, N'AQAAAAIAAYagAAAAEFQ1PjJxQLA/CcdvWH9K4MKTqNTEyqCamJkR8dtDfe4nsZ9YUm52300NHWinykzEmA==', 0, NULL, 0, '20251124 23:05:34.100', '20251113 18:23:44.190', '20260127 01:56:58.191', N'System', N'System', 0, N'Eastern Standard Time');
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[User] WHERE [UserId] = 3)
BEGIN
    INSERT INTO [dbo].[User] ([UserId], [Username], [Email], [FirstName], [LastName], [ClientId], [IsActive], [ExternalUserId], [ExternalIssuer], [PasswordHash], [IsSystemAdmin], [LastLoginDateTime], [MustChangePassword], [PasswordChangedDateTime], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted], [PreferredTimeZone])
    VALUES (3, N'editor', N'lcassin@charter.net', N'Schedule', N'Editor', 1, 0, NULL, NULL, N'AQAAAAIAAYagAAAAEC9yxm+Vsnbu/mgvW50SDHWWmdeM4cdpRvsRIQaVuwRfIt/x9jD6s+BVMjULOO17Pw==', 0, NULL, 0, '20251124 19:30:27.119', '20251113 18:23:44.203', '20260127 01:56:49.808', N'System', N'System', 0, N'Eastern Standard Time');
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[User] WHERE [UserId] = 4)
BEGIN
    INSERT INTO [dbo].[User] ([UserId], [Username], [Email], [FirstName], [LastName], [ClientId], [IsActive], [ExternalUserId], [ExternalIssuer], [PasswordHash], [IsSystemAdmin], [LastLoginDateTime], [MustChangePassword], [PasswordChangedDateTime], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted], [PreferredTimeZone])
    VALUES (4, N'jlwilson@cassinfo.com', N'jlwilson@cassinfo.com', N'Jane', N'Wilson', 1, 1, NULL, NULL, N'AQAAAAIAAYagAAAAEC8GkAV+VyRJPh0WWHyaHjEhmUsif37gFZBjDXHO1FhHjbjF6CLhElOa7ntFTeN4dQ==', 1, '20260402 18:39:03.065', 1, '20251218 19:06:34.740', '20251218 19:06:34.740', '20260402 18:39:03.065', N'Default Admin', N'jlwilson@cassinfo.com', 0, N'Central Standard Time');
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[User] WHERE [UserId] = 5)
BEGIN
    INSERT INTO [dbo].[User] ([UserId], [Username], [Email], [FirstName], [LastName], [ClientId], [IsActive], [ExternalUserId], [ExternalIssuer], [PasswordHash], [IsSystemAdmin], [LastLoginDateTime], [MustChangePassword], [PasswordChangedDateTime], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted], [PreferredTimeZone])
    VALUES (5, N'dmiller@cassinfo.com', N'dmiller@cassinfo.com', N'Dean', N'Miller', 1, 1, NULL, NULL, N'AQAAAAIAAYagAAAAEB0Ao7XJ2RSolHWwhzPQADe8+GxtcclOgV4381MDkTxcRa6O3u0cwpOjKfwCcjt3Ow==', 0, NULL, 1, '20260109 20:34:57.331', '20260109 20:34:57.331', '20260112 20:18:40.782', N'System', N'System', 0, N'Central Standard Time');
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[User] WHERE [UserId] = 6)
BEGIN
    INSERT INTO [dbo].[User] ([UserId], [Username], [Email], [FirstName], [LastName], [ClientId], [IsActive], [ExternalUserId], [ExternalIssuer], [PasswordHash], [IsSystemAdmin], [LastLoginDateTime], [MustChangePassword], [PasswordChangedDateTime], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted], [PreferredTimeZone])
    VALUES (6, N'gthomas@cassinfo.com', N'gthomas@cassinfo.com', N'Greg', N'Thomas', 1, 1, NULL, NULL, N'AQAAAAIAAYagAAAAEK7fh/5pSlFwkY1L8IZLo2Hc5RHmYqAnggbodX9pubmXSgsnh17XOekEmjgYxbL3UQ==', 0, NULL, 1, '20260109 20:36:25.902', '20260109 20:36:25.902', '20260112 20:18:59.463', N'System', N'System', 0, N'Eastern Standard Time');
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[User] WHERE [UserId] = 7)
BEGIN
    INSERT INTO [dbo].[User] ([UserId], [Username], [Email], [FirstName], [LastName], [ClientId], [IsActive], [ExternalUserId], [ExternalIssuer], [PasswordHash], [IsSystemAdmin], [LastLoginDateTime], [MustChangePassword], [PasswordChangedDateTime], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted], [PreferredTimeZone])
    VALUES (7, N'mvogel@cassinfo.com', N'mvogel@cassinfo.com', N'Michael', N'Vogel', 1, 1, NULL, NULL, N'AQAAAAIAAYagAAAAEIlEOzASy5nf1QREm2ENfHbICCggpwlYAhP9FYw4XJPR2QOfbnEcGXGBdMoHD/2usg==', 0, '20260116 19:47:35.141', 1, '20260116 18:33:03.484', '20260116 18:33:03.484', '20260116 19:47:35.141', N'System', N'mvogel@cassinfo.com', 0, N'Central Standard Time');
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[User] WHERE [UserId] = 8)
BEGIN
    INSERT INTO [dbo].[User] ([UserId], [Username], [Email], [FirstName], [LastName], [ClientId], [IsActive], [ExternalUserId], [ExternalIssuer], [PasswordHash], [IsSystemAdmin], [LastLoginDateTime], [MustChangePassword], [PasswordChangedDateTime], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted], [PreferredTimeZone])
    VALUES (8, N'mwillis@cassinfo.com', N'mwillis@cassinfo.com', N'Matthew', N'Willis', 1, 1, NULL, NULL, N'AQAAAAIAAYagAAAAENV2rjvPvuonSuxQheDSN5aTMb+YHE08MzAd2vvuoip18bfZ2CBBbyQuQL9zBFxurQ==', 1, '20260401 13:39:40.606', 1, '20260202 14:33:42.080', '20260202 14:33:42.080', '20260401 13:39:40.606', N'System', N'mwillis@cassinfo.com', 0, N'Eastern Standard Time');
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[User] WHERE [UserId] = 9)
BEGIN
    INSERT INTO [dbo].[User] ([UserId], [Username], [Email], [FirstName], [LastName], [ClientId], [IsActive], [ExternalUserId], [ExternalIssuer], [PasswordHash], [IsSystemAdmin], [LastLoginDateTime], [MustChangePassword], [PasswordChangedDateTime], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted], [PreferredTimeZone])
    VALUES (9, N'blindstrom@cassinfo.com', N'blindstrom@cassinfo.com', N'Becki', N'Lindstrom', 1, 1, NULL, NULL, N'AQAAAAIAAYagAAAAENCBDPQoqe5FrA4xQokkxYzsyMabTtVH7f96t+TAG9OyWGad22ZGZaNmIYwx87GiTg==', 0, '20260227 23:04:36.927', 1, '20260202 15:04:35.198', '20260202 15:04:35.198', '20260227 23:04:36.927', N'System', N'blindstrom@cassinfo.com', 0, N'Eastern Standard Time');
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[User] WHERE [UserId] = 10)
BEGIN
    INSERT INTO [dbo].[User] ([UserId], [Username], [Email], [FirstName], [LastName], [ClientId], [IsActive], [ExternalUserId], [ExternalIssuer], [PasswordHash], [IsSystemAdmin], [LastLoginDateTime], [MustChangePassword], [PasswordChangedDateTime], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted], [PreferredTimeZone])
    VALUES (10, N'ashea@cassinfo.com', N'ashea@cassinfo.com', N'Alexi', N'Shea', 1, 1, NULL, NULL, N'AQAAAAIAAYagAAAAEBJkUstBNH+OMMg3eiAOgN83pt8xF/flenhZqRekJqsdQa5XT8R+QQknQOg/61PEow==', 1, '20260403 13:19:49.906', 1, '20260202 20:15:38.922', '20260202 20:15:38.922', '20260403 13:19:49.906', N'System', N'ashea@cassinfo.com', 0, N'Central Standard Time');
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[User] WHERE [UserId] = 11)
BEGIN
    INSERT INTO [dbo].[User] ([UserId], [Username], [Email], [FirstName], [LastName], [ClientId], [IsActive], [ExternalUserId], [ExternalIssuer], [PasswordHash], [IsSystemAdmin], [LastLoginDateTime], [MustChangePassword], [PasswordChangedDateTime], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted], [PreferredTimeZone])
    VALUES (11, N'LBoogaard@cassinfo.com', N'LBoogaard@cassinfo.com', N'Laurie', N'Boogaard', 1, 1, NULL, NULL, N'AQAAAAIAAYagAAAAEMvOrUrQ+ry92WEk2RUy38dWMUCSp1/GbdQocaMWdoXnHLOi5YaIQJQ9+zxoWBILCw==', 0, '20260402 17:45:38.643', 1, '20260203 21:17:25.745', '20260203 21:17:25.745', '20260402 17:45:38.643', N'System', N'LBoogaard@cassinfo.com', 0, N'Central Standard Time');
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[User] WHERE [UserId] = 12)
BEGIN
    INSERT INTO [dbo].[User] ([UserId], [Username], [Email], [FirstName], [LastName], [ClientId], [IsActive], [ExternalUserId], [ExternalIssuer], [PasswordHash], [IsSystemAdmin], [LastLoginDateTime], [MustChangePassword], [PasswordChangedDateTime], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted], [PreferredTimeZone])
    VALUES (12, N'TLyday@cassinfo.com', N'TLyday@cassinfo.com', N'Teresa', N'Lyday', 1, 0, NULL, NULL, N'AQAAAAIAAYagAAAAEICye9E11c5SuurYDj/sEKwvloIa2b1iwNmNwxHmfV2qYHNz5hoIkJRR1tHb/npqbw==', 0, '20260205 16:08:26.377', 1, '20260203 21:18:08.384', '20260203 21:18:08.384', '20260306 17:44:17.741', N'System', N'System', 0, N'Central Standard Time');
END

SET IDENTITY_INSERT [dbo].[User] OFF;
GO

RAISERROR (N'[dbo].[User]: Insert Batch: 1.....Done!', 10, 1) WITH NOWAIT;
GO

-- ============================================================================
-- STEP 2: Seed UserPermissions (UserId references remapped to new IDs)
-- ============================================================================

-- UserId 1 (admin / lcassin@cassinfo.com) -- was UAT UserId 2
IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 1 AND [PermissionName] = N'scheduler')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (1, N'scheduler', NULL, NULL, 1, 1, 1, 1, 1, '20251113 18:23:44.180', '20251113 18:23:44.180', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 1 AND [PermissionName] = N'schedules')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (1, N'schedules', NULL, NULL, 1, 1, 1, 1, 1, '20251113 18:23:44.180', '20251113 18:23:44.180', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 1 AND [PermissionName] = N'jobs')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (1, N'jobs', NULL, NULL, 1, 1, 1, 1, 1, '20251113 18:23:44.180', '20251113 18:23:44.180', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 1 AND [PermissionName] = N'users:manage')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (1, N'users:manage', NULL, NULL, 1, 1, 1, 1, 0, '20251124 18:54:12.273', '20251124 18:54:12.273', N'System', N'System', 0);
END

-- UserId 2 (viewer / leecassin@icloud.com) -- was UAT UserId 3
IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 2 AND [PermissionName] = N'scheduler')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (2, N'scheduler', NULL, NULL, 0, 1, 0, 0, 0, '20251124 17:06:04.615', '20251124 17:06:04.615', N'Default Admin', N'Default Admin', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 2 AND [PermissionName] = N'schedules')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (2, N'schedules', NULL, NULL, 0, 1, 0, 0, 0, '20251124 17:06:04.616', '20251124 17:06:04.616', N'Default Admin', N'Default Admin', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 2 AND [PermissionName] = N'jobs')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (2, N'jobs', NULL, NULL, 0, 1, 0, 0, 0, '20251124 17:06:04.616', '20251124 17:06:04.616', N'Default Admin', N'Default Admin', 0);
END

-- UserId 3 (editor / lcassin@charter.net) -- was UAT UserId 4
IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 3 AND [PermissionName] = N'scheduler')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (3, N'scheduler', NULL, NULL, 1, 1, 1, 1, 0, '20260112 16:40:35.906', '20260112 16:40:35.906', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 3 AND [PermissionName] = N'schedules')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (3, N'schedules', NULL, NULL, 1, 1, 1, 1, 1, '20260112 16:40:35.948', '20260112 16:40:35.948', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 3 AND [PermissionName] = N'jobs')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (3, N'jobs', NULL, NULL, 1, 1, 1, 1, 1, '20260112 16:40:35.949', '20260112 16:40:35.949', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 3 AND [PermissionName] = N'adr')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (3, N'adr', NULL, NULL, 0, 1, 1, 0, 0, '20260112 16:40:35.949', '20260112 16:40:35.949', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 3 AND [PermissionName] = N'users:manage')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (3, N'users:manage', NULL, NULL, 0, 1, 1, 0, 0, '20260112 16:40:35.949', '20260112 16:40:35.949', N'System', N'System', 0);
END

-- UserId 4 (jlwilson@cassinfo.com) -- was UAT UserId 6
IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 4 AND [PermissionName] = N'scheduler')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (4, N'scheduler', NULL, NULL, 1, 1, 1, 1, 0, '20251218 20:30:10.607', '20251218 20:30:10.607', N'Default Admin', N'Default Admin', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 4 AND [PermissionName] = N'schedules')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (4, N'schedules', NULL, NULL, 1, 1, 1, 1, 1, '20251218 20:30:10.614', '20251218 20:30:10.614', N'Default Admin', N'Default Admin', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 4 AND [PermissionName] = N'jobs')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (4, N'jobs', NULL, NULL, 1, 1, 1, 1, 1, '20251218 20:30:10.614', '20251218 20:30:10.614', N'Default Admin', N'Default Admin', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 4 AND [PermissionName] = N'adr')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (4, N'adr', NULL, NULL, 1, 1, 1, 1, 1, '20251218 20:30:10.614', '20251218 20:30:10.614', N'Default Admin', N'Default Admin', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 4 AND [PermissionName] = N'users:manage')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (4, N'users:manage', NULL, NULL, 0, 1, 1, 0, 0, '20251218 20:30:10.614', '20251218 20:30:10.614', N'Default Admin', N'Default Admin', 0);
END

-- UserId 5 (dmiller@cassinfo.com) -- was UAT UserId 8
IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 5 AND [PermissionName] = N'scheduler')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (5, N'scheduler', NULL, NULL, 1, 1, 1, 1, 0, '20260112 15:01:30.419', '20260112 15:01:30.419', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 5 AND [PermissionName] = N'schedules')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (5, N'schedules', NULL, NULL, 1, 1, 1, 1, 1, '20260112 15:01:30.537', '20260112 15:01:30.537', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 5 AND [PermissionName] = N'jobs')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (5, N'jobs', NULL, NULL, 1, 1, 1, 1, 1, '20260112 15:01:30.539', '20260112 15:01:30.539', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 5 AND [PermissionName] = N'adr')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (5, N'adr', NULL, NULL, 1, 1, 1, 1, 1, '20260112 15:01:30.539', '20260112 15:01:30.539', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 5 AND [PermissionName] = N'users:manage')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (5, N'users:manage', NULL, NULL, 0, 1, 1, 0, 0, '20260112 15:01:30.539', '20260112 15:01:30.539', N'System', N'System', 0);
END

-- UserId 6 (gthomas@cassinfo.com) -- was UAT UserId 9
IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 6 AND [PermissionName] = N'scheduler')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (6, N'scheduler', NULL, NULL, 0, 1, 0, 0, 0, '20260109 20:36:27.017', '20260109 20:36:27.017', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 6 AND [PermissionName] = N'schedules')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (6, N'schedules', NULL, NULL, 1, 1, 1, 1, 1, '20260109 20:36:27.017', '20260109 20:36:27.017', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 6 AND [PermissionName] = N'jobs')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (6, N'jobs', NULL, NULL, 0, 1, 0, 0, 0, '20260109 20:36:27.017', '20260109 20:36:27.017', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 6 AND [PermissionName] = N'adr')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (6, N'adr', NULL, NULL, 0, 1, 1, 0, 0, '20260109 20:36:27.017', '20260109 20:36:27.017', N'System', N'System', 0);
END

-- UserId 7 (mvogel@cassinfo.com) -- was UAT UserId 10
IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 7 AND [PermissionName] = N'scheduler')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (7, N'scheduler', NULL, NULL, 0, 1, 0, 0, 0, '20260116 18:33:05.161', '20260116 18:33:05.161', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 7 AND [PermissionName] = N'schedules')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (7, N'schedules', NULL, NULL, 1, 1, 1, 1, 1, '20260116 18:33:05.165', '20260116 18:33:05.165', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 7 AND [PermissionName] = N'jobs')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (7, N'jobs', NULL, NULL, 0, 1, 0, 0, 1, '20260116 18:33:05.165', '20260116 18:33:05.165', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 7 AND [PermissionName] = N'adr')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (7, N'adr', NULL, NULL, 0, 1, 1, 0, 1, '20260116 18:33:05.165', '20260116 18:33:05.165', N'System', N'System', 0);
END

-- UserId 8 (mwillis@cassinfo.com) -- was UAT UserId 11
IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 8 AND [PermissionName] = N'scheduler')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (8, N'scheduler', NULL, NULL, 1, 1, 1, 1, 0, '20260217 19:36:11.107', '20260217 19:36:11.107', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 8 AND [PermissionName] = N'schedules')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (8, N'schedules', NULL, NULL, 1, 1, 1, 1, 1, '20260217 19:36:11.167', '20260217 19:36:11.167', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 8 AND [PermissionName] = N'jobs')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (8, N'jobs', NULL, NULL, 1, 1, 1, 1, 1, '20260217 19:36:11.167', '20260217 19:36:11.167', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 8 AND [PermissionName] = N'adr')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (8, N'adr', NULL, NULL, 1, 1, 1, 1, 1, '20260217 19:36:11.167', '20260217 19:36:11.167', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 8 AND [PermissionName] = N'users:manage')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (8, N'users:manage', NULL, NULL, 1, 1, 1, 0, 0, '20260217 19:36:11.167', '20260217 19:36:11.167', N'System', N'System', 0);
END

-- UserId 9 (blindstrom@cassinfo.com) -- was UAT UserId 12
IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 9 AND [PermissionName] = N'scheduler')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (9, N'scheduler', NULL, NULL, 1, 1, 1, 1, 0, '20260203 18:17:39.339', '20260203 18:17:39.339', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 9 AND [PermissionName] = N'schedules')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (9, N'schedules', NULL, NULL, 1, 1, 1, 1, 1, '20260203 18:17:39.339', '20260203 18:17:39.339', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 9 AND [PermissionName] = N'jobs')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (9, N'jobs', NULL, NULL, 1, 1, 1, 1, 1, '20260203 18:17:39.339', '20260203 18:17:39.339', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 9 AND [PermissionName] = N'adr')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (9, N'adr', NULL, NULL, 1, 1, 1, 1, 1, '20260203 18:17:39.339', '20260203 18:17:39.339', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 9 AND [PermissionName] = N'users:manage')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (9, N'users:manage', NULL, NULL, 1, 1, 1, 0, 0, '20260203 18:17:39.339', '20260203 18:17:39.339', N'System', N'System', 0);
END

-- UserId 10 (ashea@cassinfo.com) -- was UAT UserId 13
IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 10 AND [PermissionName] = N'scheduler')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (10, N'scheduler', NULL, NULL, 1, 1, 1, 1, 0, '20260203 19:10:17.247', '20260203 19:10:17.247', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 10 AND [PermissionName] = N'schedules')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (10, N'schedules', NULL, NULL, 1, 1, 1, 1, 1, '20260203 19:10:17.248', '20260203 19:10:17.248', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 10 AND [PermissionName] = N'jobs')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (10, N'jobs', NULL, NULL, 1, 1, 1, 1, 1, '20260203 19:10:17.248', '20260203 19:10:17.248', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 10 AND [PermissionName] = N'adr')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (10, N'adr', NULL, NULL, 1, 1, 1, 1, 1, '20260203 19:10:17.248', '20260203 19:10:17.248', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 10 AND [PermissionName] = N'users:manage')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (10, N'users:manage', NULL, NULL, 1, 1, 1, 0, 0, '20260203 19:10:17.248', '20260203 19:10:17.248', N'System', N'System', 0);
END

-- UserId 11 (LBoogaard@cassinfo.com) -- was UAT UserId 14
IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 11 AND [PermissionName] = N'scheduler')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (11, N'scheduler', NULL, NULL, 1, 1, 1, 1, 0, '20260203 21:17:27.082', '20260203 21:17:27.082', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 11 AND [PermissionName] = N'schedules')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (11, N'schedules', NULL, NULL, 1, 1, 1, 1, 1, '20260203 21:17:27.084', '20260203 21:17:27.084', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 11 AND [PermissionName] = N'jobs')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (11, N'jobs', NULL, NULL, 1, 1, 1, 1, 1, '20260203 21:17:27.084', '20260203 21:17:27.084', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 11 AND [PermissionName] = N'users:manage')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (11, N'users:manage', NULL, NULL, 1, 1, 1, 0, 0, '20260203 21:17:27.084', '20260203 21:17:27.084', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 11 AND [PermissionName] = N'adr')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (11, N'adr', NULL, NULL, 1, 1, 1, 1, 1, '20260203 21:17:27.084', '20260203 21:17:27.084', N'System', N'System', 0);
END

-- UserId 12 (TLyday@cassinfo.com) -- was UAT UserId 15
IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 12 AND [PermissionName] = N'scheduler')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (12, N'scheduler', NULL, NULL, 1, 1, 1, 1, 0, '20260306 17:44:12.323', '20260306 17:44:12.323', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 12 AND [PermissionName] = N'schedules')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (12, N'schedules', NULL, NULL, 1, 1, 1, 1, 1, '20260306 17:44:12.360', '20260306 17:44:12.360', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 12 AND [PermissionName] = N'jobs')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (12, N'jobs', NULL, NULL, 1, 1, 1, 1, 1, '20260306 17:44:12.360', '20260306 17:44:12.360', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 12 AND [PermissionName] = N'adr')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (12, N'adr', NULL, NULL, 1, 1, 1, 1, 1, '20260306 17:44:12.360', '20260306 17:44:12.360', N'System', N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserId] = 12 AND [PermissionName] = N'users:manage')
BEGIN
    INSERT INTO [dbo].[UserPermission] ([UserId], [PermissionName], [ResourceType], [ResourceId], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])
    VALUES (12, N'users:manage', NULL, NULL, 1, 1, 1, 0, 0, '20260306 17:44:12.360', '20260306 17:44:12.360', N'System', N'System', 0);
END

RAISERROR (N'[dbo].[UserPermission]: Insert Batch: 1.....Done!', 10, 1) WITH NOWAIT;
GO
