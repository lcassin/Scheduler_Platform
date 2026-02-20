USE [TMOS]
GO
/****** Object:  StoredProcedure [dbo].[psp_Report_FundingReview_Textron]    Script Date: 2/20/2026 4:49:14 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
--exec psp_Report_FundingReview_Textron @FundingDate = '12/11/2025'

ALTER procedure [dbo].[psp_Report_FundingReview_Textron] 
	@FundingDate NVARCHAR(MAX)=NULL
as 

begin 
--***************************************************************************************
--[psp_Report_FundingReview_Textron] 
-- Author:      wes18846
-- Create date: 07/12/17 
-- Description: Textron's weekly/bi-weekly Funding Review Report
-- Used By:		FundingReport_Textron.aspx.cs
-- Version:     1.0
--********************* Maintenance Log *************************************************
-- YYYY-MM-DD 	Version 	Author		Desco
-- 2017-08-24	1.1			AMS			MADE UPDATES TO LOGIC TO LOOK FOR SPECIAL FUNDING DATE FIRST
--										THEN LOOK AT NOTIFICATION DATE IF THE SPECIAL FUNDING DATE IS NULL.
--										ALSO CHANGED THE >GETDATE() IN THE WHERE CLAUSE TO = CONVERT(NVARCHAR(10),GETDATE(),101)
--										TO ACCOMODATE THE MISSING TIME IN THE SPECIAL FUNDING DATE AND NOTIFICATION DATE FIELDS.
-- 2017-10-30   1.2         CJW         Exclude certain Accounts in first pass. Get them in second pass
--										because they have to be broken out by costcenter, not circuit
-- 2021/03/05				gthomas		made sure all invoices have currency value for export
-- 2022/09/12				LCASSIN		Add FundingDate Variable to allow running report for previous dat
-- 2023/05/01				LCASSIN		#30565 - Add Master Account 8310009607307  to list and Allow Multiple Funding Dates
-- 2024/02/08				rchapman	#41924 - correct allocations for account number change
-- 2024/02/22				lcassin		#41924 - fix issues with missing entities on special formatted accounts
-- 2024/05/24				lcassin		#47267 - move joins to an update because causing multiple duplications on some charges
-- 2025/04/01				lcassin		#52287 - fix split record issue where taxes and charges are on separate GLLinkID's
-- 2026/02/20				lcassin		#52287 - fix regression: scope GLLinkID self-join to BillerInvoiceId and Account records only
---  Backup Report File Locations N:\nCONTROL\Client Files - Active Clients\Textron\Weekly Funding Report
--***************************************************************

	IF @FundingDate IS NULL SET @FundingDate=CONVERT(NVARCHAR(MAX), GETDATE(), 101)
	
	if object_id('tempdb..##Final_TextronFunding_Rpt','u') is not null 
		drop table ##Final_TextronFunding_Rpt

	if object_id('tempdb..[#tmpMR]','u') is not null 
		drop table #tmpMR	
			
	if object_id('tempdb..[#tmpCircData]','u') is not null 
		drop table #tmpCircData		
		
	if object_id('tempdb..[#tmpInvDiff]','u') is not null 
		drop table #tmpInvDiff

	if object_id('tempdb..[#tmpBIDS]','u') is not null 
		drop table #tmpBIDS

	if object_id('tempdb..[#tmpSpecialBIDS]','u') is not null 
			drop table #tmpSpecialBIDS

	DECLARE @ClientNo varchar(8)
	Select @ClientNo = '428202'; -- 428202 TEXTRON

	DECLARE @sql nvarchar(4000) = ''

	-- Create temp table of BillerInvoiceIds, Invoice NUmbers, and Funding Dates that will be included on report

	CREATE TABLE #tmpBIDS(
		BillerInvoiceId numeric(18,0)
		,InvoiceNumber nvarchar(50)
		,Funding_Date nvarchar(15)
		,BillingMonth smalldatetime
	)

	CREATE TABLE #tmpSpecialBIDS(
		BillerInvoiceId numeric(18,0)
		,InvoiceNumber nvarchar(50)
		,Funding_Date nvarchar(15)
		,BillingMonth smalldatetime
	)

	--Get Base data for invoices to include
	INSERT INTO #tmpBIDS(BillerInvoiceId, InvoiceNumber, Funding_Date, BillingMonth)
	Select bi.BillerInvoiceId, bi.InvoiceNumber, CONVERT(nvarchar,ISNULL(fb.Special_Funding_date,fb.Notification_date),101) as Funding_Date, bi.BillingMonth
	from tbl_ALL_BillerInvoice bi WITH (NOLOCK)
	INNER JOIN tbl_EDI_PmtRemit_824_FromBank fb WITH (NOLOCK)
		ON fb.BillerInvoiceId = bi.BillerInvoiceId
            AND fb.OTI_AppAckCode<>'TR'
	Where Client_Number = @ClientNo
	--and ISNULL(fb.Special_Funding_date,fb.Notification_date) = '03/09/2020'  --Testing
	and ISNULL(fb.Special_Funding_date,fb.Notification_date) IN (SELECT Value FROM String_Split(@FundingDate, ','))  
	and ISNULL(fb.Special_Funding_date,fb.Notification_date) IS NOT NULL
	AND BI.Client_Vendor_MA_ID IN (SELECT Client_Vendor_MA_ID FROM aaprofitlabclientvendorsAccounts WITH (NOLOCK) WHERE Active=1 AND RTRIM(ISNULL(AliasName, '')) = '' AND ProfitLabClientNumber=@ClientNo)
	
    ----------------------------
	-- Get Special Invoices that were excluded in #tmpBIDs 
	INSERT INTO #tmpSpecialBIDS(BillerInvoiceId, InvoiceNumber, Funding_Date, BillingMonth)
	Select bi.BillerInvoiceId, bi.InvoiceNumber, CONVERT(nvarchar,ISNULL(fb.Special_Funding_date,fb.Notification_date),101) as Funding_Date, bi.BillingMonth
	from tbl_ALL_BillerInvoice bi WITH (NOLOCK)
	INNER JOIN tbl_EDI_PmtRemit_824_FromBank fb WITH (NOLOCK)
		ON fb.BillerInvoiceId = bi.BillerInvoiceId
         AND fb.OTI_AppAckCode<>'TR'
	Where Client_Number = @ClientNo
	--and ISNULL(fb.Special_Funding_date,fb.Notification_date) = '03/09/2020'  --Testing
	and ISNULL(fb.Special_Funding_date,fb.Notification_date) IN (SELECT Value FROM String_Split(@FundingDate, ','))  
	and ISNULL(fb.Special_Funding_date,fb.Notification_date) IS NOT NULL
	AND BI.Client_Vendor_MA_ID IN (SELECT Client_Vendor_MA_ID FROM aaprofitlabclientvendorsAccounts  WITH (NOLOCK) WHERE Active=1 AND RTRIM(ISNULL(AliasName, '')) <> '' AND CLient_Number=@ClientNo)
	

	--Get all monthly rec info where the monthlyrec invoice number and billing month mathches #tmpBIDS invoice number and billing month
	Select sum(Origcharge) as charges, mr.BillingMonth, Client_Number
			,mr.invoicenumber, mr.MasterAccountNumber, stationorcircuit, Vendor, mr.GLLinkID, mr.BillerInvoiceID
	INTO #tmpMR
	from tbl_all_monthlyrec mr WITH (NOLOCK)
	INNER JOIN #tmpBIDS tmp WITH (NOLOCK)
		ON tmp.BillingMonth = mr.BillingMonth
		AND tmp.InvoiceNumber = mr.InvoiceNumber
	where CLient_Number = @ClientNo
		AND mr.InvoiceNumber in (Select DISTINCT InvoiceNumber from #tmpBIDS)
	group by Client_Number, mr.MasterAccountNumber, mr.BillerInvoiceID, mr.InvoiceNumber, Vendor, stationorcircuit, mr.BillingMonth, mr.GLLinkID
	--HAVING sum(Origcharge) <> 0


	select 	DISTINCT ROW_NUMBER () OVER (PARTITION BY ci.GLLINKID,ci.StationOrCircuit Order BY ci.GLLINKID,ci.StationOrCircuit) as rowNr
			,rp.ProfileId, EmployeeId, EmployeeFirstName, EmployeeLastName, ci.StationOrCircuit
			,ci.gllinkid, ct.CircuitTypeDesc as InventoryType, rp.CostCenter
			, ci.LocationId, ci.CircuitInvId, ci.CircuitDesc, ci.Client_Number
	INTO #tmpCircData
	from CircuitInv ci WITH (NOLOCK)
	left OUTER join (SELECT MAX(InventoryId) as InventoryId, CircuitInvId FROM tbl_Link_ProfileInventory_CircuitInv WITH (NOLOCK)
								GROUP BY CircuitInvID) pci
		on ci.CircuitInvId = pci.CircuitInvId 
	left OUTER join tbl_Request_ProfileInventory rpi WITH (NOLOCK)
		on rpi.InventoryId = pci.InventoryId
	left OUTER join tbl_request_profile rp WITH (NOLOCK)
		on rp.ProfileId = rpi.ProfileId
		and rp.CLient_Number = @ClientNo
	LEFT OUTER JOIN tbl_ref_std_Codes_CircuitType ct
		ON CONVERT(nvarchar,ct.CircuitTypeId) = ci.CircuitTypeId
		AND ct.Client_NUmber = @ClientNo
	Where ci.Client_Number = @ClientNo


	DELETE FROM #tmpCircData where rowNr > 1  -- DELETE CircuitInv rows with duplicate GLLINKIDS to only have one row when joining chargesumallocated to remove duplicate info


	CREATE TABLE ##Final_TextronFunding_Rpt
	(
		BillerInvoiceId numeric(18,0)
		, Business_Unit nvarchar(255) null
		, Company nvarchar(255) null
		, [GL Cost Account] nvarchar(255) null
		, Currency nvarchar(10) null
		, MasterAccountNo nvarchar(255) null
		, Account_Number nvarchar(255) null
		, Sub_Billing nvarchar(255) null
		, GLLinkId numeric(18,0)
		, vendor nvarchar(255) null
		, InvoiceNumber nvarchar(255) null
		, AccountDesc nvarchar(255) null
		, InvoiceDate smalldatetime null
		, BillingMonth smalldatetime null
		, CostCenterId numeric(18,0) null
		, CostCenter nvarchar(255) null
		, EntityName nvarchar(255) null
		, Invoice_Total money null
		, Allocated_Amount money null
		, Identifier nvarchar(255) null
		, Inventory_Type nvarchar(255) null
		, Circuit_Desc nvarchar(255) null
		, Batch_Name nvarchar(255) null
		, First_Name nvarchar(255) null
		, Last_Name nvarchar(255) null
		, EmployeeID nvarchar(255) null
		, City nvarchar(255) null
		, [State] nvarchar(255) null
		, Address_Line nvarchar(255) null
	 )
 
	Insert INTO ##Final_TextronFunding_Rpt
	(
		BillerInvoiceId 
		, Business_Unit
		, Company
		, [GL Cost Account]
		, Currency
		, MasterAccountNo
		, Account_Number
		, Sub_Billing
		, GLLinkId
		, vendor
		, InvoiceNumber
		, AccountDesc
		, InvoiceDate
		, BillingMonth
		, CostCenterId
		, CostCenter
		, EntityName
		, Invoice_Total
		, Allocated_Amount
		, Identifier
		, Inventory_Type 
		, Circuit_Desc 
		, Batch_Name
		, First_name
		, Last_Name
		, EmployeeId
		, City
		, [State]
		, Address_Line
	)

	Select DISTINCT bi.BillerInvoiceId
			,null
			,null
			,null
			,null
			, MasterAccountNo
			,null
			,null
			, mr.GLLinkID
			, bi.Vendor
			, bi.InvoiceNumber
			, isnull(cva.AcctDesc, '') as AccountDesc
			, bi.InvoiceDate
			, bi.BillingMonth
			,null
			,null
			, cl.Description
			, TotalOrig as Invoice_Total
			, mr.Charges
			, mr.StationOrCircuit
			, InventoryType
			, CircuitDesc
			, tmp.Funding_Date as Batch_Name
			, cd.EmployeeFirstName
			, cd.EmployeeLastName
			, cd.EmployeeId
			, City
			, [State]
			, RTRIM(ISNULL(cl.AddressInfo1, '') + ' ' + ISNULL(cl.AddressInfo2, ''))
	from #tmpBIDS tmp
	INNER JOIN tbl_ALL_BillerInvoice bi WITH (NOLOCK)
		ON Bi.BillerInvoiceId = tmp.BillerInvoiceId
	LEFT OUTER JOIN #tmpMR mr WITH (NOLOCK)
		on mr.BillingMOnth = bi.BillingMonth
		AND mr.Client_Number = @ClientNo
		AND mr.BillerInvoiceID=bi.BillerInvoiceID
	LEFT OUTER JOIN #tmpCircData cd WITH (NOLOCK)
		on cd.stationorcircuit = mr.StationorCircuit
		AND cd.GLLinkID = mr.GLLinkID
		AND mr.Client_Number = cd.Client_Number
	LEFT OUTER JOIN aaprofitlabclientlocation cl WITH (NOLOCK)
		ON cl.LocationId = cd.LocationId
		AND cl.Client_Number = @ClientNo
	LEFT OUTER JOIN  aaProfitlabClientVendorsAccounts cva WITH (NOLOCK)
		ON cva.Client_Vendor_MA_ID = bi.Client_Vendor_MA_ID
		AND cva.ProfitlabClientNumber = @ClientNo

	----------------------------------------------------------------------
	Insert INTO ##Final_TextronFunding_Rpt
		(
			BillerInvoiceId 
			, Business_Unit
			, Company
			, [GL Cost Account]
			, Currency
			, MasterAccountNo
			, Account_Number
			, Sub_Billing
			, GLLinkId
			, vendor
			, InvoiceNumber
			, AccountDesc
			, InvoiceDate
			, BillingMonth
			, CostCenterId
			, CostCenter
			, EntityName
			, Invoice_Total
			, Allocated_Amount
			, Identifier
			, Inventory_Type 
			, Circuit_Desc 
			, Batch_Name
			, First_name
			, Last_Name
			, EmployeeId
			, City
			, [State]
			, Address_Line
		)
		Select DISTINCT bi.BillerInvoiceId, null,null,null,null, MasterAccountNo,null,null, csa.GLLinkId, bi.Vendor, bi.InvoiceNumber
				, isnull(cva.AcctDesc, '') as AccountDesc, bi.InvoiceDate, bi.BillingMonth,cc.CostCenterId,cc.Description, NULL as EntityName, TotalOrig as Invoice_Total
				, sum(csa.OrigCharge) as charges, null as Identifier
				, CVA.AliasName  as Inventory_Type
				, NULL AS CircuitDesc, tmp.Funding_Date as Batch_Name
				, NULL AS EmployeeFirstName, NULL AS EmployeeLastName, NULL AS EmployeeID, NULL AS City
				, NULL AS [State]
				, NULL AS AddressLine 
		from #tmpSpecialBIDS tmp
		INNER JOIN tbl_ALL_BillerInvoice bi WITH (NOLOCK)
			ON Bi.BillerInvoiceId = tmp.BillerInvoiceId
		LEFT OUTER JOIN tbl_All_monthlyRec csa WITH (NOLOCK)  
			on csa.BillerInvoiceId = tmp.BillerInvoiceId
		LEFT OUTER JOIN vj_CostCentertoGLLinkIDbyClientMonth LNK WITH (NOLOCK)
			ON CSA.Client_Number=LNK.Client_Number
				AND CSA.BillerInvoiceID=LNK.BillerInvoiceID
				AND CSA.GLLinkID=LNK.GLLinkID
		LEFT OUTER JOIN CostCenter cc WITH (NOLOCK)
			ON cc.Client_Number=LNK.Client_Number
			AND cc.Level1ID = LNK.Level1ID
			AND ISNULL(cc.Level2ID, 0) = ISNULL(lnk.Level2ID, 0)
			AND ISNULL(cc.Level3ID, 0) = ISNULL(lnk.Level3ID, 0)
			AND ISNULL(cc.Level4ID, 0) = ISNULL(lnk.Level4ID, 0)
			AND ISNULL(cc.Level5ID, 0) = ISNULL(lnk.Level5ID, 0)
			AND ISNULL(cc.Level6ID, 0) = ISNULL(lnk.Level6ID, 0)
			AND ISNULL(cc.Level7ID, 0) = ISNULL(lnk.Level7ID, 0)
			AND cc.Record_Sts=0
		LEFT OUTER JOIN  aaProfitlabClientVendorsAccounts cva WITH (NOLOCK)
			ON cva.Client_Vendor_MA_ID = bi.Client_Vendor_MA_ID
			AND cva.ProfitlabClientNumber = @ClientNo
		Group by bi.BillerInvoiceId, MasterAccountNo, bi.Vendor, bi.InvoiceNumber, cva.AcctDesc, bi.InvoiceDate, bi.BillingMonth
			, TotalOrig, tmp.Funding_Date, cc.CostCenterId, cc.Description 
			, csa.GLLinkId, CVA.AliasName
	
    
	UPDATE ##Final_TextronFunding_Rpt
		SET First_Name=NULLIF(RTRIM(ISNULL(cd.EmployeeFirstName,rp.EmployeeFirstName)), ''),
			Last_Name=NULLIF(RTRIM(ISNULL(cd.EmployeeLastName,rp.EmployeeLastName)), ''),
			EmployeeID=NULLIF(RTRIM(ISNULL(cd.EmployeeID, rp.EmployeeID)), ''),
			EntityName=NULLIF(RTRIM(ISNULL(cl.Description, '')), ''),
			Circuit_Desc=NULLIF(RTRIM(ISNULL(cd.CircuitDesc, '')), ''),
			city=NULLIF(RTRIM(ISNULL(cl.City, '')), ''),
			[State]=NULLIF(RTRIM(ISNULL(cl.[State], '')), ''),
			Address_Line=NULLIF(RTRIM(ISNULL(LTRIM(RTRIM(ISNULL(cl.AddressInfo1, '') + ' ' + ISNULL(cl.AddressInfo2, ''))), '')), '')
	FROM ##Final_TextronFunding_Rpt R
	LEFT OUTER JOIN tbl_link_Profile_GLAcct gl WITH (NOLOCK)
		on gl.GLLinkId = R.GLLinkId
	LEFT OUTER JOIN tbl_request_profile rp WITH (NOLOCK)
		on rp.ProfileID = gl.ProfileID
		and rp.Client_Number = @ClientNo
	LEFT OUTER JOIN #tmpCircData cd
		on cd.GLLinkID = R.GLLinkID
	LEFT OUTER JOIN aaprofitlabclientlocation cl WITH (NOLOCK)
		ON cl.LocationId = cd.LocationId
		AND cl.Client_Number = @ClientNo

	--Get the charge amount that is allocated to the company level. (No monthly rec Record)
	Select bi.BillingMonth, bi.InvoiceNumber,totalOrig as InvTotal,(totalOrig - sum(isnull(mr.OrigCharge,0))) as InvoiceDiff, tmp.BillerInvoiceID, CF.CurrencyCode
	INTO #tmpInvDiff
	from #tmpBIDS tmp WITH (NOLOCK)
	INNER JOIN tbl_ALL_BillerInvoice bi WITH (NOLOCK)
		ON bi.BillerInvoiceId = tmp.BillerInvoiceId
	INNER JOIN CurrencyFormats CF WITH (NOLOCK) 
		ON bi.CurrencyID = CF.CurrencyID
	LEFT JOIN tbl_ALL_MonthlyRec mr WITH (NOLOCK)
		ON mr.InvoiceNumber = bi.InvoiceNumber
		AND mr.BillingMonth = bi.BillingMonth
		AND mr.Client_Number = @ClientNo
	Where bi.Client_Number = @ClientNo
		AND bi.InvoiceNumber in (Select DISTINCT InvoiceNumber from ##Final_TextronFunding_Rpt)
   GROUP BY bi.BillingMonth,bi.InvoiceNumber, totalOrig, tmp.BillerInvoiceID, CF.CurrencyCode

   --Get the charge amount that is allocated to the company level. (No monthly rec Record)
	INSERT INTO #tmpInvDiff
	Select bi.BillingMonth, bi.InvoiceNumber,totalOrig as InvTotal,(totalOrig - sum(isnull(mr.OrigCharge,0))) as InvoiceDiff, tmp.BillerInvoiceID, CF.CurrencyCode
	from #tmpSpecialBIDS tmp WITH (NOLOCK)
	INNER JOIN tbl_ALL_BillerInvoice bi WITH (NOLOCK)
		ON bi.BillerInvoiceId = tmp.BillerInvoiceId
	INNER JOIN CurrencyFormats CF WITH (NOLOCK) 
		ON bi.CurrencyID = CF.CurrencyID
	LEFT JOIN tbl_ALL_MonthlyRec mr WITH (NOLOCK)
		ON mr.InvoiceNumber = bi.InvoiceNumber
		AND mr.BillingMonth = bi.BillingMonth
		AND mr.Client_Number = @ClientNo
	Where bi.Client_Number = @ClientNo
		AND bi.InvoiceNumber in (Select DISTINCT InvoiceNumber from ##Final_TextronFunding_Rpt)
   GROUP BY bi.BillingMonth,bi.InvoiceNumber, totalOrig, tmp.BillerInvoiceID, CF.CurrencyCode

	--Insert records for account level charges
	INSERT INTO ##Final_TextronFunding_Rpt(BillerInvoiceId,Business_Unit,Company,[GL Cost Account],Currency,MasterAccountNo,Account_Number
											,Sub_Billing,GLLinkId,vendor,InvoiceNumber,AccountDesc,InvoiceDate,BillingMonth,CostCenterId
											,CostCenter,EntityName,Invoice_Total,Allocated_Amount,Identifier,Inventory_Type,Circuit_Desc
											,Batch_Name,First_Name,Last_Name,EmployeeID,City,State,Address_Line) --29
	Select BillerInvoiceID,null,null,null,CurrencyCode,null,null,null,null,null,InvoiceNumber,null,null,BillingMonth,null,null,null,null,InvoiceDiff
			,'Account','Account',null,null,null,null,null,null,null,null
	from #tmpInvDiff WITH (NOLOCK)

	
	UPDATE ##Final_TextronFunding_Rpt
		SET GLLinkId = AL.GLLinkID
	FROM ##Final_TextronFunding_Rpt FT
		INNER JOIN tbl_All_ChargesumAllocated AL WITH (NOLOCK)
		ON  AL.BillerInvoiceId = FT.BillerInvoiceId
			AND AL.Client_Number=@ClientNo
		INNER JOIN aaProfitLabClientGLListing GL WITH (NOLOCK)
			ON AL.GLID=GL.GLID
			AND GL.[GL Cost Account]=FT.[GL Cost Account]
			AND GL.Client_Number=@ClientNo
	WHERE FT.GLLinkId IS NULL AND AL.GlLinkID IN (SELECT GLLinkID FROM CircuitInv WITH (NOLOCK) WHERE Client_Number=@ClientNo)

	--Select * from ##Final_TextronFunding_Rpt

	UPDATE ##Final_TextronFunding_Rpt
	SET CostCenterId = AL.CostCenterId
	from ##Final_TextronFunding_Rpt tmp
		INNER JOIN tbl_All_ChargesumAllocated AL WITH (NOLOCK)
	   ON AL.BillerInvoiceId = tmp.BillerInvoiceId
		And AL.GLLinkId = tmp.GLLinkId
		AND tmp.CostCenterId IS NULL
	
	UPDATE r2
	Set Batch_Name = r1.Batch_Name
	FROM ##Final_TextronFunding_Rpt r1 WITH (NOLOCK)
	inner join ##Final_TextronFunding_Rpt r2 WITH (NOLOCK)
		ON r2.BillerInvoiceId = r1.BillerInvoiceId
		AND (r1.Inventory_Type  <> 'Account' OR r1.Inventory_Type IS NULL)
	Where r2.Inventory_Type = 'Account' 

	UPDATE r2
	Set CostCenterId = r1.CostCenterId
	FROM ##Final_TextronFunding_Rpt r1 WITH (NOLOCK)
	inner join ##Final_TextronFunding_Rpt r2 WITH (NOLOCK)
		ON r2.BillerInvoiceId = r1.BillerInvoiceId
		AND (r1.Inventory_Type  <> 'Account' OR r1.Inventory_Type IS NULL)
	Where r2.Inventory_Type = 'Account'  AND r1.CostCenterId IS NULL

	UPDATE r2
	Set GLLinkId = r1.GLLinkId
	FROM ##Final_TextronFunding_Rpt r1 WITH (NOLOCK)
	inner join ##Final_TextronFunding_Rpt r2 WITH (NOLOCK)
		ON r2.BillerInvoiceId = r1.BillerInvoiceId
		AND (r1.Inventory_Type  <> 'Account' OR r1.Inventory_Type IS NULL)
	Where r2.Inventory_Type = 'Account' AND r1.GLLinkId IS NULL

	UPDATE ##Final_TextronFunding_Rpt
	SET [GL Cost Account] = GL.[GL Cost Account]
	from ##Final_TextronFunding_Rpt a 
    INNER JOIN tbl_All_ChargesumAllocated alloc
        on a.BillerInvoiceID=alloc.BillerInvoiceID
            AND a.GLLinkID=alloc.GLLinkID
            AND alloc.Client_Number=@ClientNo
    INNER JOIN aaprofitlabclientGLListing GL
        ON alloc.GLID=GL.GLID
	
	UPDATE ##Final_TextronFunding_Rpt
	SET Currency = CurrencyFormats.CurrencyCode
	FROM CurrencyFormats WITH (NOLOCK)
	INNER JOIN tbl_All_ChargesumAllocated WITH (NOLOCK)
		ON tbl_All_ChargesumAllocated.CurrencyID = CurrencyFormats.CurrencyID
	Where tbl_All_ChargesumAllocated.BillerInvoiceId = ##Final_TextronFunding_Rpt.BillerInvoiceId
		And tbl_All_ChargesumAllocated.GLLinkId = ##Final_TextronFunding_Rpt.GLLinkId

	UPDATE ##Final_TextronFunding_Rpt
	Set Account_Number = btn
		, Sub_Billing = EAN
	from GLAcct WITH (NOLOCK)
	Where GLAcct.GLLinkID = ##Final_TextronFunding_Rpt.GLLinkId
		and GLAcct.MasterAcctNo = ##Final_TextronFunding_Rpt.MasterAccountNo
		
	UPDATE ##Final_TextronFunding_Rpt
	SET Identifier = Sub_Billing
	Where BillerInvoiceId in (
		Select BillerInvoiceId from #tmpSpecialBIDS)


	UPDATE ##Final_TextronFunding_Rpt
	Set EntityName = b.Description
	from ##Final_TextronFunding_Rpt a
	inner join CostCenter b WITH (NOLOCK)
	on b.CostCenterID = a.CostCenterId
	WHERE EntityName IS NULL

	UPDATE ##Final_TextronFunding_Rpt
	Set CostCenter = Level4
	from CostCenter WITH (NOLOCK)
	inner join PasswordLevel4Listing WITH (NOLOCK)
		ON PasswordLevel4Listing.Level4ID = COstCenter.Level4ID
		AND PasswordLevel4Listing.Client_Number = @ClientNo
	Where CostCenter.CostCenterId = ##Final_TextronFunding_Rpt.CostCenterId 

	UPDATE ##Final_TextronFunding_Rpt
	Set Business_Unit = PasswordLevel2Listing.Description
	from CostCenter WITH (NOLOCK)
	inner join PasswordLevel2Listing WITH (NOLOCK)
		ON PasswordLevel2Listing.level2Id = COstCenter.Level2Id
		AND PasswordLevel2Listing.Client_Number = @ClientNo
	Where CostCenter.CostCenterId = ##Final_TextronFunding_Rpt.CostCenterId 

	UPDATE ##Final_TextronFunding_Rpt
	Set Company = PasswordLevel3Listing.Description
	from CostCenter WITH (NOLOCK)
	inner join PasswordLevel3Listing WITH (NOLOCK)
		ON PasswordLevel3Listing.level3Id = COstCenter.Level3Id
		AND PasswordLevel3Listing.Client_Number = @ClientNo
	Where CostCenter.CostCenterId = ##Final_TextronFunding_Rpt.CostCenterId 

	UPDATE r2 
	SET Currency = (Select top 1 r1.Currency)
		,MasterAccountNo= (Select top 1 r1.MasterAccountNo)
		,Invoice_Total = (Select top 1 r1.Invoice_Total)
		,AccountDesc = (Select top 1 r1.AccountDesc)
		,Account_Number = (Select top 1 r1.Account_Number)
		,Vendor = (Select top 1 r1.vendor)
		,InvoiceDate = (Select top 1 r1.InvoiceDate)
	FROM ##Final_TextronFunding_Rpt r1 WITH (NOLOCK)
	inner join ##Final_TextronFunding_Rpt r2 WITH (NOLOCK)
		ON r2.BillerInvoiceId = r1.BillerInvoiceId
		AND (r1.Inventory_Type  <> 'Account' OR r1.Inventory_Type IS NULL)
	Where r2.Inventory_Type = 'Account'

    UPDATE ##Final_TextronFunding_Rpt SET Identifier=src.Identifier,
                                          Inventory_Type=src.Inventory_Type
    FROM ##Final_TextronFunding_Rpt tmp
        INNER JOIN (SELECT acct.BillerInvoiceId,
                      acct.GLLinkID,
                      circ.Identifier,
                      circ.Inventory_Type
                    FROM ##Final_TextronFunding_Rpt acct
                    INNER JOIN ##Final_TextronFunding_Rpt circ
                        ON acct.GLLinkID = circ.GLLinkId
                        AND acct.BillerInvoiceId = circ.BillerInvoiceId
                    WHERE acct.Identifier = 'Account'
                      AND circ.Identifier <> 'Account') src
            ON tmp.GLLinkID = src.GLLinkID
            AND tmp.BillerInvoiceId = src.BillerInvoiceId
            AND tmp.Identifier = 'Account'


	DELETE from ##Final_TextronFunding_Rpt where (Allocated_Amount = 0 or Allocated_Amount IS NULL)

	Select [BillerInvoiceId],
    [Business_Unit],
    [Company],
    [GL Cost Account],
    [Currency],
    [MasterAccountNo],
    [Account_Number],
    [Sub_Billing],
    MAX([GLLinkId]) as [GLLinkID],
    [vendor],
    [InvoiceNumber],
    [AccountDesc],
    [InvoiceDate],
    [BillingMonth],
    [CostCenterId],
    [CostCenter],
    [EntityName],
    MAX([Invoice_Total]) AS [Invoice_Total],
    SUM([Allocated_Amount]) AS [Allocated_Amount],
    [Identifier],
    [Inventory_Type],
    NULLIF(RTRIM(ISNULL([Circuit_Desc], '')), '') as [Circuit_Desc],
    [Batch_Name],
    NULLIF(RTRIM(ISNULL([First_Name], '')), '') as [First_Name],
    NULLIF(RTRIM(ISNULL([Last_Name], '')), '') as [Last_Name],
    NULLIF(RTRIM(ISNULL([EmployeeID], '')), '') as [EmployeeID],
    NULLIF(RTRIM(ISNULL([City], '')), '') as [City],
    NULLIF(RTRIM(ISNULL([State], '')), '') as [State],
    NULLIF(RTRIM(ISNULL([Address_Line], '')), '') as [Address_Line]	  From ##Final_TextronFunding_Rpt
    GROUP BY
    [BillerInvoiceId],
    [Business_Unit],
    [Company],
    [GL Cost Account],
    [Currency],
    [MasterAccountNo],
    [Account_Number],
    [Sub_Billing],
	[vendor],
    [InvoiceNumber],
    [AccountDesc],
    [InvoiceDate],
    [BillingMonth],
    [CostCenterId],
    [CostCenter],
    [EntityName],
    [Identifier],
    [Inventory_Type],
    NULLIF(RTRIM(ISNULL([Circuit_Desc], '')), ''),
    [Batch_Name],
    NULLIF(RTRIM(ISNULL([First_Name], '')), ''),
    NULLIF(RTRIM(ISNULL([Last_Name], '')), ''),
    NULLIF(RTRIM(ISNULL([EmployeeID], '')), ''),
    NULLIF(RTRIM(ISNULL([City], '')), ''),
    NULLIF(RTRIM(ISNULL([State], '')), ''),
    NULLIF(RTRIM(ISNULL([Address_Line], '')), '')
   
    ORDER BY Vendor,MasterAccountNo,Sub_Billing,Identifier
	-------------------------Call the webpage to build and save xlsx-------------------------------------------
	-- Email is sent from web page

	DECLARE @ipAddress NVARCHAR(100)
	SET @ipAddress = '10.0.3.103'

	DECLARE @uri varchar(2000), @viewresults nvarchar(20) = null
	SET @uri = 'https://clmcorp.cassinfo.com/clm/reporting/External/FundingReport_Textron.aspx?fundingDate=' + REPLACE(convert(nvarchar(10),(SELECT TOP 1 Value FROM STRING_SPLIT(@FundingDate, ',')),101), '/', '-')

	declare @hr int 
	declare @object int 
	declare @src int 
	declare @desc varchar(1000) 

	EXEC @hr=sp_oacreate  'Msxml2.ServerXMLHTTP.6.0', @object out

	IF @hr <> 0  AND @viewresults is null
	BEGIN 
		EXEC sp_OAGetErrorInfo @object, @src OUT, @desc OUT 
		SELECT hr=convert(varbinary(4),@hr), Source=@src, Description=@desc 
	END

	EXEC @hr = sp_oamethod @object,'open',null,'GET',@uri

	EXEC @hr= sp_oamethod @object,'send',null 
	
	--------------Drop Global temp table----------------
	if object_id('tempdb..##Final_TextronFunding_Rpt','u') is not null 
              drop table ##Final_TextronFunding_Rpt
	-- grant execute on psp_Report_FundingReview_Textron to StoredProcs;
end;  
