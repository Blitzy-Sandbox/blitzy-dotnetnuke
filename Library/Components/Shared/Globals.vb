'
' DotNetNuke® - http://www.dotnetnuke.com
' Copyright (c) 2002-2008
' by DotNetNuke Corporation
'
' Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
' documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
' the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
' to permit persons to whom the Software is furnished to do so, subject to the following conditions:
'
' The above copyright notice and this permission notice shall be included in all copies or substantial portions 
' of the Software.
'
' THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
' TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
' THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
' CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
' DEALINGS IN THE SOFTWARE.
'
Imports System
Imports System.Collections.Specialized
Imports System.ComponentModel
Imports System.IO
Imports System.Net
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Runtime.Serialization.Formatters.Binary
Imports System.Threading
Imports System.Xml
Imports System.Xml.Serialization
Imports System.Web
Imports System.Web.Mail
Imports System.Web.UI
Imports ICSharpCode.SharpZipLib.Zip
Imports DotNetNuke.Framework.Providers
Imports DotNetNuke.Entities.Tabs
Imports DotNetNuke.Entities.Modules
Imports DotNetNuke.Entities.Portals
Imports DotNetNuke.Security
Imports DotNetNuke.Security.Roles
Imports DotNetNuke.Services.FileSystem
Imports DotNetNuke.Services.Localization
Imports DotNetNuke.UI.Utilities
Imports DotNetNuke.UI.Skins

Namespace DotNetNuke.Common

    ''' -----------------------------------------------------------------------------
    ''' Namespace:  DotNetNuke.Common
    ''' Project:    DotNetNuke
    ''' Module:     Globals
    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' This module contains global utility functions, constants, and enumerations.
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    '''		[cnurse]	11/16/2004	documented
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Module Globals

#Region "Public Enums"

        Public Enum PerformanceSettings
            'The values of the enum are used to calculate
            'cache settings throughout the portal.
            'Calculating based on these numbers keeps 
            'the scaling linear for all caching.
            NoCaching = 0
            LightCaching = 1
            ModerateCaching = 3
            HeavyCaching = 6
        End Enum

        Public Enum UpgradeStatus
            Upgrade
            Install
            None
            [Error]
        End Enum

        Public Enum PortalRegistrationType
            NoRegistration = 0
            PrivateRegistration = 1
            PublicRegistration = 2
            VerifiedRegistration = 3
        End Enum

#End Region

#Region "Public Constants"

        Public Const glbRoleAllUsers As String = "-1"
        Public Const glbRoleSuperUser As String = "-2"
        Public Const glbRoleUnauthUser As String = "-3"
        Public Const glbRoleNothing As String = "-4"

        Public Const glbRoleAllUsersName As String = "All Users"
        Public Const glbRoleSuperUserName As String = "Superuser"
        Public Const glbRoleUnauthUserName As String = "Unauthenticated Users"

        Public Const glbDefaultPage As String = "Default.aspx"
        Public Const glbHostSkinFolder As String = "_default"

        ' TODO:  These constants are deprecated but cannot be removed until the next batch of breaking change
        ' ****************************************************************************************
        ' Constants are inlined in code and would require a rebuild of any module or skinobject
        ' that may be using these constants.
        ' ****************************************************************************************
        Public Const glbDefaultSkinFolder As String = "/DNN-Blue/"
        Public Const glbDefaultSkin As String = "Horizontal Menu - Fixed Width.ascx"
        Public Const glbDefaultAdminSkin As String = "Horizontal Menu - Fixed Width.ascx"
        Public Const glbDefaultContainerFolder As String = "/DNN-Blue/"
        Public Const glbDefaultContainer As String = "Image Header - Color Background.ascx"
        Public Const glbDefaultAdminContainer As String = "Image Header - Color Background.ascx"
        ' ****************************************************************************************

        Public Const glbDefaultControlPanel As String = "Admin/ControlPanel/IconBar.ascx"
        Public Const glbDefaultPane As String = "ContentPane"
        Public Const glbImageFileTypes As String = "jpg,jpeg,jpe,gif,bmp,png,swf"
        Public Const glbConfigFolder As String = "\Config\"
        Public Const glbAboutPage As String = "about.htm"
        Public Const glbDotNetNukeConfig As String = "DotNetNuke.config"


        Public Const glbSuperUserAppName As Integer = -1

        Public Const glbProtectedExtension As String = ".resources"

        Public Const glbEmailRegEx As String = "\b[a-zA-Z0-9._%\-+']+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,4}\b"

#End Region

#Region "Private Members"

        ' global constants for the life of the application ( set in Application_Start )
        Private _ApplicationPath As String
        Private _ApplicationMapPath As String
        Private _HostMapPath As String
        Private _HostPath As String
        Private _ServerName As String
        Private _HostSettings As Hashtable
        Private _PerformanceSetting As PerformanceSettings

        Private strWebFarmEnabled As String = ""

        ' constants lazy loaded since the data may not exist until after an upgrade
        Private _defaultSkin As SkinDefaults
        Private _defaultContainer As SkinDefaults

#End Region

#Region "Public Properties"


        Public ReadOnly Property DefaultSkin() As SkinDefaults
            Get
                If _defaultSkin Is Nothing Then
                    _defaultSkin = New SkinDefaults(SkinDefaultType.SkinInfo)
                End If
                Return _defaultSkin
            End Get
        End Property

        Public ReadOnly Property DefaultContainer() As SkinDefaults
            Get
                If _defaultContainer Is Nothing Then
                    _defaultContainer = New SkinDefaults(SkinDefaultType.ContainerInfo)
                End If
                Return _defaultContainer
            End Get
        End Property

        Public Property ApplicationPath() As String
            Get
                Return _ApplicationPath
            End Get
            Set(ByVal Value As String)
                _ApplicationPath = Value
            End Set
        End Property

        Public Property ApplicationMapPath() As String
            Get
                Return _ApplicationMapPath
            End Get
            Set(ByVal Value As String)
                _ApplicationMapPath = Value
            End Set
        End Property

        Public Property HostMapPath() As String
            Get
                Return _HostMapPath
            End Get
            Set(ByVal Value As String)
                _HostMapPath = Value
            End Set
        End Property

        Public Property HostPath() As String
            Get
                Return _HostPath
            End Get
            Set(ByVal Value As String)
                _HostPath = Value
            End Set
        End Property

        Public Property ServerName() As String
            Get
                Return _ServerName
            End Get
            Set(ByVal Value As String)
                _ServerName = Value
            End Set
        End Property

        Public ReadOnly Property HostSettings() As Hashtable
            Get
                Return Entities.Host.HostSettings.GetHostSettings
            End Get
        End Property

        Public ReadOnly Property PerformanceSetting() As PerformanceSettings
            Get
                Return CType(Convert.ToInt32(IIf(Common.Globals.HostSettings("PerformanceSetting") Is Nothing, 3, Common.Globals.HostSettings("PerformanceSetting"))), PerformanceSettings)
            End Get
        End Property

        Public ReadOnly Property WebFarmEnabled() As Boolean
            Get
                If String.IsNullOrEmpty(strWebFarmEnabled) Then
                    If Config.GetSetting("EnableWebFarmSupport") Is Nothing Then
                        strWebFarmEnabled = "false"
                    Else
                        strWebFarmEnabled = Config.GetSetting("EnableWebFarmSupport").ToLower
                    End If
                End If
                Return Boolean.Parse(strWebFarmEnabled)
            End Get
        End Property

#End Region

#Region "Public Methods"

        Public Function BuildCrossTabDataSet(ByVal DataSetName As String, ByVal result As IDataReader, ByVal FixedColumns As String, ByVal VariableColumns As String, ByVal KeyColumn As String, ByVal FieldColumn As String, ByVal FieldTypeColumn As String, ByVal StringValueColumn As String, ByVal NumericValueColumn As String) As DataSet
            Return BuildCrossTabDataSet(DataSetName, result, FixedColumns, VariableColumns, KeyColumn, FieldColumn, FieldTypeColumn, StringValueColumn, NumericValueColumn, System.Globalization.CultureInfo.CurrentCulture)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' converts a data reader with serialized fields into a typed data set
        ''' </summary>
        ''' <param name="DataSetName">Name of the dataset to be created</param>
        ''' <param name="result">Data reader that contains all field values serialized</param>
        ''' <param name="FixedColumns">List of fixed columns, delimited by commas. Columns must be contained in DataReader</param>
        ''' <param name="VariableColumns">List of variable columns, delimited by commas. Columns must be contained in DataReader</param>
        ''' <param name="KeyColumn">Name of the column, that contains the row ID. Column must be contained in DataReader</param>
        ''' <param name="FieldColumn">Name of the column, that contains the field name. Column must be contained in DataReader</param>
        ''' <param name="FieldTypeColumn">Name of the column, that contains the field type name. Column must be contained in DataReader</param>
        ''' <param name="StringValueColumn">Name of the column, that contains the field value, if stored as string. Column must be contained in DataReader</param>
        ''' <param name="NumericValueColumn">Name of the column, that contains the field value, if stored as number. Column must be contained in DataReader</param>
        ''' <param name="culture">culture of the field values in data reader's string value column</param>
        ''' <returns>The generated DataSet</returns>
        ''' <history>
        ''' 	[sleupold]     08/24/2006	Created temporary clone of core function and added support for culture based parsing of numeric values
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function BuildCrossTabDataSet(ByVal DataSetName As String, ByVal result As IDataReader, ByVal FixedColumns As String, ByVal VariableColumns As String, ByVal KeyColumn As String, ByVal FieldColumn As String, ByVal FieldTypeColumn As String, ByVal StringValueColumn As String, ByVal NumericValueColumn As String, ByVal Culture As System.Globalization.CultureInfo) As DataSet

            Dim arrFixedColumns As String() = Nothing
            Dim arrVariableColumns As String() = Nothing
            Dim arrField As String()
            Dim FieldType As String
            Dim intColumn As Integer
            Dim intKeyColumn As Integer

            ' create dataset
            Dim crosstab As New DataSet(DataSetName)
            crosstab.Namespace = "NetFrameWork"

            ' create table
            Dim tab As New DataTable(DataSetName)

            ' split fixed columns
            arrFixedColumns = FixedColumns.Split(",".ToCharArray())

            ' add fixed columns to table
            For intColumn = LBound(arrFixedColumns) To UBound(arrFixedColumns)
                arrField = arrFixedColumns(intColumn).Split("|".ToCharArray())
                Dim col As New DataColumn(arrField(0), System.Type.GetType("System." & arrField(1)))
                tab.Columns.Add(col)
            Next intColumn

            ' split variable columns
            If VariableColumns <> "" Then
                arrVariableColumns = VariableColumns.Split(",".ToCharArray())

                ' add varible columns to table
                For intColumn = LBound(arrVariableColumns) To UBound(arrVariableColumns)
                    arrField = arrVariableColumns(intColumn).Split("|".ToCharArray())
                    Dim col As New DataColumn(arrField(0), System.Type.GetType("System." & arrField(1)))
                    col.AllowDBNull = True
                    tab.Columns.Add(col)
                Next intColumn
            End If

            ' add table to dataset
            crosstab.Tables.Add(tab)

            ' add rows to table
            intKeyColumn = -1
            Dim row As DataRow = Nothing
            While result.Read()
                ' loop using KeyColumn as control break
                If Convert.ToInt32(result(KeyColumn)) <> intKeyColumn Then
                    ' add row
                    If intKeyColumn <> -1 Then
                        tab.Rows.Add(row)
                    End If

                    ' create new row
                    row = tab.NewRow()

                    ' assign fixed column values
                    For intColumn = LBound(arrFixedColumns) To UBound(arrFixedColumns)
                        arrField = arrFixedColumns(intColumn).Split("|".ToCharArray())
                        row(arrField(0)) = result(arrField(0))
                    Next intColumn

                    ' initialize variable column values
                    If VariableColumns <> "" Then
                        For intColumn = LBound(arrVariableColumns) To UBound(arrVariableColumns)
                            arrField = arrVariableColumns(intColumn).Split("|".ToCharArray())
                            Select Case arrField(1)
                                Case "Decimal"
                                    row(arrField(0)) = 0
                                Case "String"
                                    row(arrField(0)) = ""
                            End Select
                        Next intColumn
                    End If

                    intKeyColumn = Convert.ToInt32(result(KeyColumn))
                End If

                ' assign pivot column value
                If FieldTypeColumn <> "" Then
                    FieldType = result(FieldTypeColumn).ToString
                Else
                    FieldType = "String"
                End If
                Select Case FieldType
                    Case "Decimal"       ' decimal
                        row(Convert.ToInt32(result(FieldColumn))) = result(NumericValueColumn)
                    Case "String"       ' string
                        If Culture Is System.Globalization.CultureInfo.CurrentCulture Then
                            row(result(FieldColumn).ToString) = result(StringValueColumn)
                        Else
                            Select Case tab.Columns(result(FieldColumn).ToString).DataType.ToString
                                Case "System.Decimal", "System.Currency"
                                    row(result(FieldColumn).ToString) = Decimal.Parse(result(StringValueColumn).ToString, Culture)
                                Case "System.Int32"
                                    row(result(FieldColumn).ToString) = Int32.Parse(result(StringValueColumn).ToString, Culture)
                                Case Else
                                    row(result(FieldColumn).ToString) = result(StringValueColumn)
                            End Select
                        End If
                End Select
            End While

            result.Close()

            ' add row
            If intKeyColumn <> -1 Then
                tab.Rows.Add(row)
            End If

            ' finalize dataset
            crosstab.AcceptChanges()

            ' return the dataset
            Return crosstab

        End Function

        ' convert datareader to dataset
        Public Function ConvertDataReaderToDataSet(ByVal reader As IDataReader) As DataSet

            ' add datatable to dataset
            Dim objDataSet As New DataSet
            objDataSet.Tables.Add(ConvertDataReaderToDataTable(reader))

            Return objDataSet

        End Function

        ' convert datareader to dataset
        Public Function ConvertDataReaderToDataTable(ByVal reader As IDataReader) As DataTable

            ' create datatable from datareader
            Dim objDataTable As New DataTable
            Dim intFieldCount As Integer = reader.FieldCount
            Dim intCounter As Integer
            For intCounter = 0 To intFieldCount - 1
                objDataTable.Columns.Add(reader.GetName(intCounter), reader.GetFieldType(intCounter))
            Next intCounter

            ' populate datatable
            objDataTable.BeginLoadData()
            Dim objValues(intFieldCount - 1) As Object
            While reader.Read()
                reader.GetValues(objValues)
                objDataTable.LoadDataRow(objValues, True)
            End While
            reader.Close()
            objDataTable.EndLoadData()

            Return objDataTable

        End Function

        ' returns the absolute server path to the root ( ie. D:\Inetpub\wwwroot\directory\ )
        Public Function GetAbsoluteServerPath(ByVal Request As HttpRequest) As String
            Dim strServerPath As String

            strServerPath = Request.MapPath(Request.ApplicationPath)
            If Not strServerPath.EndsWith("\") Then
                strServerPath += "\"
            End If

            GetAbsoluteServerPath = strServerPath
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the ApplicationName for the MemberRole API.
        ''' </summary>
        ''' <remarks>
        ''' This overload is used to get the current ApplcationName.  The Application
        ''' Name is in the form Prefix_Id, where Prefix is the object qualifier
        ''' for this instance of DotNetNuke, and Id is the current PortalId for normal
        ''' users or glbSuperUserAppName for SuperUsers.
        ''' </remarks>
        ''' <history>
        '''		[cnurse]	01/18/2005	documented and modifeid to handle a Prefix
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function GetApplicationName() As String
            Dim appName As String

            If Convert.ToString(HttpContext.Current.Items("ApplicationName")) = "" Then
                'No Application Name saved
                Dim _PortalSettings As PortalSettings = PortalController.GetCurrentPortalSettings
                If _PortalSettings Is Nothing Then
                    'No Name is defined and no portal is current so return "/"
                    appName = "/"
                Else
                    'Get the "default' Application Name based on the PortalId
                    appName = GetApplicationName(_PortalSettings.PortalId)
                End If
            Else
                appName = Convert.ToString(HttpContext.Current.Items("ApplicationName"))
            End If

            Return appName

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the ApplicationName for the MemberRole API.
        ''' </summary>
        ''' <remarks>
        ''' This overload is used to build the Application Name from the Portal Id
        ''' </remarks>
        ''' <history>
        '''		[cnurse]	01/18/2005	documented and modifeid to handle a Prefix
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function GetApplicationName(ByVal PortalID As Integer) As String

            Dim appName As String

            'Get the Data Provider Configuration
            Dim _providerConfiguration As ProviderConfiguration = ProviderConfiguration.GetProviderConfiguration("data")

            ' Read the configuration specific information for the current Provider
            Dim objProvider As Provider = CType(_providerConfiguration.Providers(_providerConfiguration.DefaultProvider), Provider)

            'Get the Object Qualifier frm the Provider Configuration
            Dim _objectQualifier As String = objProvider.Attributes("objectQualifier")
            If _objectQualifier <> "" And _objectQualifier.EndsWith("_") = False Then
                _objectQualifier += "_"
            End If

            appName = _objectQualifier + Convert.ToString(PortalID)

            Return appName
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetDatabaseVersion - gets the current version in the DB
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <returns>The DB Version</returns>
        ''' <history>
        ''' 	[cnurse]	02/02/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function GetDatabaseVersion() As String
            Return GetDatabaseVersion("")
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetDatabaseVersion - gets the current version in the DB
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="separator">The separator used to separate the version parts (eg 4.4.1)</param>
        ''' <returns>The DB Version</returns>
        ''' <history>
        ''' 	[cnurse]	02/02/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function GetDatabaseVersion(ByVal separator As String) As String

            Dim strDatabaseVersion As String = ""

            Try
                Dim dr As IDataReader = PortalSettings.GetDatabaseVersion
                If dr.Read Then
                    strDatabaseVersion = Format(dr("Major"), "00") & separator & Format(dr("Minor"), "00") & separator & Format(dr("Build"), "00")
                End If
                dr.Close()
            Catch ex As Exception
                strDatabaseVersion = "ERROR:" & ex.Message
            End Try

            Return strDatabaseVersion

        End Function

        Public Function GetDomainName(ByVal Request As HttpRequest) As String
            Return GetDomainName(Request, False)
        End Function

        ' returns the domain name of the current request ( ie. www.domain.com or 207.132.12.123 or www.domain.com/directory if subhost )
        ' Actually, more to the point, returns the URL for the portal of this request.url.
        '   USE:
        '       Returns URI appropriate for checking against Portal Aliases.
        '   ASSUMPTIONS:
        '       portal access is always centric thru *.aspx or *.axd file;
        '       DotNetNuke application directory names are special (and not part of portal alias);
        '           so only specific DNN top level directory names are examined
        Public Function GetDomainName(ByVal Request As HttpRequest, ByVal ParsePortNumber As Boolean) As String
            Dim DomainName As StringBuilder = New StringBuilder
            Dim URL() As String
            Dim URI As String   ' holds Request.Url, less the "?" parameters
            Dim intURL As Integer

            ' split both URL separater, and parameter separator
            ' We trim right of '?' so test for filename extensions can occur at END of URL-componenet.
            ' Test:   'www.aspxforum.net'  should be returned as a valid domain name.
            ' just consider left of '?' in URI
            ' Binary, else '?' isn't taken literally; only interested in one (left) string
            URI = Request.Url.ToString()
            Dim hostHeader As String = Config.GetSetting("HostHeader")
            If Not hostHeader Is Nothing Then
                If hostHeader.Length > 0 Then
                    URI = URI.ToLower.Replace(hostHeader.ToLower, "")
                End If
            End If
            intURL = InStr(URI, "?", CompareMethod.Binary)
            If intURL > 0 Then
                URI = Left(URI, intURL - 1)
            End If

            URL = Split(URI, "/")

            For intURL = 2 To URL.GetUpperBound(0)
                Select Case URL(intURL).ToLower
                    Case "admin", "controls", "desktopmodules", "mobilemodules", "premiummodules", "providers"
                        Exit For
                    Case Else
                        ' exclude filenames ENDing in ".aspx" or ".axd" --- 
                        '   we'll use reverse match,
                        '   - but that means we are checking position of left end of the match;
                        '   - and to do that, we need to ensure the string we test against is long enough;
                        If (URL(intURL).Length >= ".aspx".Length) Then      'long enough for both tests
                            If InStrRev(URL(intURL).ToLower(), ".aspx") = (URL(intURL).Length - (".aspx".Length - 1)) _
                            Or InStrRev(URL(intURL).ToLower(), ".axd") = (URL(intURL).Length - (".axd".Length - 1)) _
                            Or InStrRev(URL(intURL).ToLower(), ".ashx") = (URL(intURL).Length - (".ashx".Length - 1)) Then
                                Exit For
                            End If
                        End If
                        ' non of the exclusionary names found
                        DomainName.Append(IIf(DomainName.ToString <> "", "/", "").ToString & URL(intURL))
                End Select
            Next intURL

            ' handle port specification
            If ParsePortNumber Then
                If DomainName.ToString.IndexOf(":") <> -1 Then
                    Dim usePortNumber As Boolean = (Not Request.IsLocal)
                    If Not Utilities.Config.GetSetting("UsePortNumber") Is Nothing Then
                        usePortNumber = Boolean.Parse(Utilities.Config.GetSetting("UsePortNumber"))
                    End If
                    If usePortNumber = False Then
                        DomainName = DomainName.Replace(":" & Request.Url.Port.ToString(), "")
                    End If
                End If
            End If

            Return DomainName.ToString

        End Function

        ' get list of files from folder matching criteria
        Public Function GetFileList() As ArrayList
            Return GetFileList(-1, "", True, "", False)
        End Function

        Public Function GetFileList(ByVal PortalId As Integer) As ArrayList
            Return GetFileList(PortalId, "", True, "", False)
        End Function

        Public Function GetFileList(ByVal PortalId As Integer, ByVal strExtensions As String) As ArrayList
            Return GetFileList(PortalId, strExtensions, True, "", False)
        End Function

        Public Function GetFileList(ByVal PortalId As Integer, ByVal strExtensions As String, ByVal NoneSpecified As Boolean) As ArrayList
            Return GetFileList(PortalId, strExtensions, NoneSpecified, "", False)
        End Function

        Public Function GetFileList(ByVal PortalId As Integer, ByVal strExtensions As String, ByVal NoneSpecified As Boolean, ByVal Folder As String) As ArrayList
            Return GetFileList(PortalId, strExtensions, NoneSpecified, Folder, False)
        End Function

        Public Function GetFileList(ByVal PortalId As Integer, ByVal strExtensions As String, ByVal NoneSpecified As Boolean, ByVal Folder As String, ByVal includeHidden As Boolean) As ArrayList
            Dim arrFileList As New ArrayList

            If NoneSpecified Then
                arrFileList.Add(New FileItem("", "<" + Localization.GetString("None_Specified") + ">"))
            End If

            Dim portalRoot As String
            If PortalId = Null.NullInteger Then
                portalRoot = HostMapPath
            Else
                Dim objPortals As New PortalController
                Dim objPortal As PortalInfo = objPortals.GetPortal(PortalId)
                portalRoot = objPortal.HomeDirectoryMapPath
            End If

            Dim objFolder As FolderInfo = FileSystemUtils.GetFolder(PortalId, Folder)
            If Not objFolder Is Nothing Then
                Dim objFiles As New FileController
                Dim dr As IDataReader = objFiles.GetFiles(PortalId, objFolder.FolderID)
                While dr.Read()
                    If InStr(1, strExtensions.ToUpper, dr("Extension").ToString.ToUpper) <> 0 Or strExtensions = "" Then
                        Dim filePath As String = (portalRoot & dr("Folder").ToString & dr("fileName").ToString).Replace("/", "\")
                        Dim StorageLocation As Integer = 0

                        If Not dr("StorageLocation") Is Nothing Then
                            StorageLocation = CType(dr("StorageLocation"), Integer)
                            Select Case StorageLocation
                                Case 1 : filePath = filePath & glbProtectedExtension ' Secure File System
                                Case 2 ' Secure Database
                                Case Else ' Insecure File System
                            End Select
                        End If

                        ' check if file exists - as the database may not be synchronized with the file system
                        ' Make sure its not a file stored in the db, if it is we don't worry about seeing if it exists
                        If Not StorageLocation = 2 Then
                            If File.Exists(filePath) Then
                                ' check if file is hidden
                                If includeHidden Then
                                    arrFileList.Add(New FileItem(dr("FileID").ToString, dr("FileName").ToString))
                                Else
                                    Dim attributes As System.IO.FileAttributes = File.GetAttributes(filePath)
                                    If Not ((attributes And FileAttributes.Hidden) = FileAttributes.Hidden) Then
                                        arrFileList.Add(New FileItem(dr("FileID").ToString, dr("FileName").ToString))
                                    End If
                                End If
                            End If
                        Else
                            ' File is stored in DB - Just add to arraylist
                            arrFileList.Add(New FileItem(dr("FileID").ToString, dr("FileName").ToString))
                        End If
                        'END Change
                    End If
                End While
                dr.Close()
            End If

            Return arrFileList
        End Function

        Public Function GetHostPortalSettings() As PortalSettings
            Dim TabId As Integer = -1
            Dim PortalId As Integer = -1

            Dim objPortalAliasInfo As PortalAliasInfo = Nothing

            ' if the portal alias exists
            If Convert.ToString(HostSettings("HostPortalId")) <> "" Then
                ' use the host portal
                objPortalAliasInfo = New PortalAliasInfo
                objPortalAliasInfo.PortalID = Integer.Parse(Convert.ToString(HostSettings("HostPortalId")))

                PortalId = objPortalAliasInfo.PortalID
            End If

            ' load the PortalSettings into current context
            Return New PortalSettings(TabId, objPortalAliasInfo)
        End Function

        ' retrieves the domain name of the portal ( ie. http://www.domain.com " )
        Public Function GetPortalDomainName(ByVal strPortalAlias As String, Optional ByVal Request As HttpRequest = Nothing, Optional ByVal blnAddHTTP As Boolean = True) As String

            Dim strDomainName As String = ""

            Dim strURL As String = ""
            Dim arrPortalAlias() As String
            Dim intAlias As Integer

            If Not Request Is Nothing Then
                strURL = GetDomainName(Request)
            End If

            arrPortalAlias = Split(strPortalAlias, ",")
            For intAlias = 0 To arrPortalAlias.Length - 1
                If arrPortalAlias(intAlias) = strURL Then
                    strDomainName = arrPortalAlias(intAlias)
                End If
            Next
            If strDomainName = "" Then
                strDomainName = arrPortalAlias(0)
            End If

            If blnAddHTTP Then
                strDomainName = AddHTTP(strDomainName)
            End If

            GetPortalDomainName = strDomainName

        End Function

        Public Function GetPortalSettings() As PortalSettings

            Dim portalSettings As PortalSettings = Nothing

            'Try getting the settings from the Context
            If Not HttpContext.Current Is Nothing Then
                portalSettings = CType(HttpContext.Current.Items("PortalSettings"), PortalSettings)
            End If

            ' If nothing then try getting the Host Settings
            If portalSettings Is Nothing Then
                portalSettings = GetHostPortalSettings()
            End If

            Return portalSettings

        End Function

        Public Function GetPortalTabs(ByVal intPortalId As Integer, ByVal blnNoneSpecified As Boolean, ByVal blnHidden As Boolean, ByVal blnDeleted As Boolean, ByVal blnURL As Boolean, ByVal bCheckAuthorised As Boolean) As ArrayList
            Return GetPortalTabs(intPortalId, True, blnNoneSpecified, blnHidden, blnDeleted, blnURL, bCheckAuthorised)
        End Function

        Public Function GetPortalTabs(ByVal intPortalId As Integer, ByVal blnIncludeActiveTab As Boolean, ByVal blnNoneSpecified As Boolean, ByVal blnHidden As Boolean, ByVal blnDeleted As Boolean, ByVal blnURL As Boolean, ByVal bCheckAuthorised As Boolean) As ArrayList
            Dim objTabs As New TabController
            Dim arrTabs As ArrayList
            Dim excludeTabId As Integer = Null.NullInteger

            ' Obtain current PortalSettings from Current Context
            Dim _portalSettings As PortalSettings = PortalController.GetCurrentPortalSettings

            'Get the portal's tabs
            If intPortalId = _portalSettings.PortalId Then
                'Load current Portals tabs into arrTabs
                arrTabs = _portalSettings.DesktopTabs
            Else
                'We are editing a different portal (as host) so get the portal's tabs
                arrTabs = objTabs.GetTabs(intPortalId)
            End If

            If Not blnIncludeActiveTab Then
                excludeTabId = _portalSettings.ActiveTab.TabID
            End If

            Return GetPortalTabs(arrTabs, excludeTabId, blnNoneSpecified, blnHidden, blnDeleted, blnURL, bCheckAuthorised)
        End Function

        Public Function GetPortalTabs(ByVal objDesktopTabs As ArrayList, ByVal blnNoneSpecified As Boolean, ByVal blnHidden As Boolean) As ArrayList
            Return GetPortalTabs(objDesktopTabs, -1, blnNoneSpecified, blnHidden, False, False, False)
        End Function

        Public Function GetPortalTabs(ByVal objDesktopTabs As ArrayList, ByVal blnNoneSpecified As Boolean, ByVal blnHidden As Boolean, ByVal blnDeleted As Boolean, ByVal blnURL As Boolean) As ArrayList
            Return GetPortalTabs(objDesktopTabs, -1, blnNoneSpecified, blnHidden, blnDeleted, blnURL, False)
        End Function

        Public Function GetPortalTabs(ByVal objDesktopTabs As ArrayList, ByVal currentTab As Integer, ByVal blnNoneSpecified As Boolean, ByVal blnHidden As Boolean, ByVal blnDeleted As Boolean, ByVal blnURL As Boolean, ByVal bCheckAuthorised As Boolean) As ArrayList
            Dim arrPortalTabs As ArrayList = New ArrayList
            Dim objTab As TabInfo

            If blnNoneSpecified Then
                objTab = New TabInfo
                objTab.TabID = -1
                objTab.TabName = "<" + Services.Localization.Localization.GetString("None_Specified") + ">"
                objTab.TabOrder = 0
                objTab.ParentId = -2
                arrPortalTabs.Add(objTab)
            End If

            Dim intCounter As Integer
            Dim strIndent As String

            For Each objTab In objDesktopTabs
                If (currentTab < 0) Or (objTab.TabID <> currentTab) Then
                    If objTab.IsAdminTab = False And (objTab.IsVisible = True Or blnHidden = True) And (objTab.IsDeleted = False Or blnDeleted = True) And (objTab.TabType = TabType.Normal Or blnURL = True) Then
                        Dim tabTemp As TabInfo = objTab.Clone
                        strIndent = ""
                        For intCounter = 1 To tabTemp.Level
                            strIndent += "..."
                        Next
                        tabTemp.TabName = strIndent & tabTemp.TabName
                        If bCheckAuthorised Then
                            'Check if User has Administrator rights to this tab
                            If PortalSecurity.IsInRoles(tabTemp.AdministratorRoles) Then
                                arrPortalTabs.Add(tabTemp)
                            End If
                        Else
                            arrPortalTabs.Add(tabTemp)
                        End If
                    End If

                End If
            Next

            Return arrPortalTabs
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Returns the folder path under the root for the portal 
        ''' </summary>
        ''' <param name="strFileNamePath">The folder the absolute path</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        '''   [cnurse] 16/9/2004  Updated for localization, Help and 508
        '''   [Philip Beadle] 6 October 2004 Moved to Globals from WebUpload.ascx.vb so can be accessed by URLControl.ascx
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function GetSubFolderPath(ByVal strFileNamePath As String, ByVal portalId As Integer) As String
            Dim ParentFolderName As String
            If portalId = Null.NullInteger Then
                ParentFolderName = Common.Globals.HostMapPath.Replace("/", "\")
            Else
                ' Get Portal
                Dim objPortals As New PortalController
                Dim objPortal As PortalInfo = objPortals.GetPortal(portalId)

                ParentFolderName = objPortal.HomeDirectoryMapPath.Replace("/", "\")
            End If
            Dim strFolderpath As String = strFileNamePath.Substring(0, strFileNamePath.LastIndexOf("\") + 1)

            GetSubFolderPath = strFolderpath.Substring(ParentFolderName.Length).Replace("\", "/")
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The GetTotalRecords method gets the number of Records returned.
        ''' </summary>
        ''' <param name="dr">An <see cref="IDataReader"/> containing the Total no of records</param>
        ''' <returns>An Integer</returns>
        ''' <history>
        ''' 	[cnurse]	02/01/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function GetTotalRecords(ByRef dr As IDataReader) As Integer

            Dim total As Integer = 0

            If dr.Read Then
                Try
                    total = Convert.ToInt32(dr("TotalRecords"))
                Catch ex As Exception
                    total = -1
                End Try
            End If

            Return total

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUpgradeStatus - determines whether an upgrade/install is required
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <returns>An UpgradeStatus enum Upgrade/Install/None</returns>
        ''' <history>
        ''' 	[cnurse]	02/02/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function GetUpgradeStatus() As UpgradeStatus

            Dim strAssemblyVersion As String = glbAppVersion.Replace(".", "")
            Dim strDatabaseVersion As String = ""
            Dim strConfirmVersion As String = ""
            Dim xmlConfig As New XmlDocument

            Dim status As UpgradeStatus = UpgradeStatus.None


            ' first call GetProviderPath - this insures that the Database is Initialised correctly
            'and also generates the appropriate error message if it cannot be initialised correctly
            Dim strProviderPath As String = PortalSettings.GetProviderPath()

            ' get current database version from DB
            If Not strProviderPath.StartsWith("ERROR:") Then
                strDatabaseVersion = GetDatabaseVersion()
            Else
                strDatabaseVersion = strProviderPath
            End If

            If strDatabaseVersion.StartsWith("ERROR") Then
                If IsInstalled() Then
                    'Errors connecting to the database after an initial installation should be treated as errors.
                    status = UpgradeStatus.Error
                Else
                    'An error that occurs before the database has been installed should be treated as a new install
                    status = UpgradeStatus.Install
                End If
            ElseIf strDatabaseVersion = "" Then
                'No Db Version so Install
                status = UpgradeStatus.Install
            Else
                If strDatabaseVersion <> strAssemblyVersion Then
                    'Upgrade Required - confirm by checking the DB Version in the DB
                    status = UpgradeStatus.Upgrade
                End If
            End If

            Return status

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' ImportFile - converts a file url (/Portals/0/somefile.gif) to the appropriate 
        ''' FileID=xx identification for use in importing portals, tabs and modules
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <returns>An UpgradeStatus enum Upgrade/Install/None</returns>
        ''' <history>
        ''' 	[cnurse]	10/11/2007	moved from PortalController so the same 
        '''                             logic can be used by Module and Tab templates
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function ImportFile(ByVal PortalId As Integer, ByVal url As String) As String

            Dim strUrl As String = url

            If GetURLType(url) = TabType.File Then
                Dim objFileController As New FileController
                Dim fileId As Integer = objFileController.ConvertFilePathToFileId(url, PortalId)
                If fileId >= 0 Then
                    strUrl = "FileID=" + fileId.ToString
                End If
            End If

            Return strUrl
        End Function

        ''' <summary>
        ''' IsInstalled looks at various file artifacts to determine if DotNetNuke has already been installed.
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks>
        ''' If DotNetNuke has been installed, then we should treat database connection errors as real errors.  
        ''' If DotNetNuke has not been installed, then we should expect to have database connection problems
        ''' since the connection string may not have been configured yet, which can occur during the installation
        ''' wizard.
        ''' </remarks>
        Private Function IsInstalled() As Boolean
            Const c_PassingScore As Integer = 4

            Dim installationdatefactor As Integer = CType(IIf(HasInstallationDate(), 1, 0), Integer)
            Dim dataproviderfactor As Integer = CType(IIf(HasDataProviderLogFiles(), 3, 0), Integer)
            Dim htmlmodulefactor As Integer = CType(IIf(ModuleDirectoryExists("html"), 2, 0), Integer)
            Dim portaldirectoryfactor As Integer = CType(IIf(HasNonDefaultPortalDirectory(), 2, 0), Integer)
            'Dim directInstallfactor As Integer = CType(IIf(IsInstallationURL(), c_PassingScore, 0), Integer)
            Dim localexecutionfactor As Integer = CType(IIf(HttpContext.Current.Request.IsLocal, c_PassingScore - 1, 0), Integer)

            'This calculation ensures that you have a more than one item that indicates you have already installed DNN.
            'While it is possible that you might not have an installation date or that you have deleted log files
            'it is unlikely that you have removed every trace of an installation and yet still have a working install
            Return (Not IsInstallationURL()) AndAlso ((installationdatefactor + dataproviderfactor + htmlmodulefactor + portaldirectoryfactor + localexecutionfactor) >= c_PassingScore)
        End Function

        Private Function HasDataProviderLogFiles() As Boolean
            Dim currentdataprovider As Providers.Provider = Config.GetDefaultProvider("data")
            Dim providerpath As String = currentdataprovider.Attributes("providerPath")

            'If the provider path does not exist, then there can't be any log files
            If Not String.IsNullOrEmpty(providerpath) Then
                providerpath = HttpContext.Current.Server.MapPath(providerpath)
                If Directory.Exists(providerpath) Then
                    Return Directory.GetFiles(providerpath, "*.log").Length > 0
                End If
            End If

            Return False
        End Function

        Private Function HasInstallationDate() As Boolean
            Return CType(IIf(Config.GetSetting("InstallationDate") Is Nothing, False, True), Boolean)
        End Function

        Private Function ModuleDirectoryExists(ByVal ModuleName As String) As Boolean
            Dim dir As String = Globals.ApplicationMapPath & "\desktopmodules\" & ModuleName
            Return Directory.Exists(dir)
        End Function

        Private Function HasNonDefaultPortalDirectory() As Boolean
            Dim dir As String = Globals.ApplicationMapPath & "\portals"
            If Directory.Exists(dir) Then
                Return Directory.GetDirectories(dir).Length > 1
            End If
            Return False
        End Function

        Private Function IsInstallationURL() As Boolean
            Dim requestURL As String = HttpContext.Current.Request.RawUrl.ToLowerInvariant

            Return requestURL.Contains("\install.aspx") Or requestURL.Contains("\installwizard.aspx")
        End Function

        ' encodes a URL for posting to an external site
        Public Function HTTPPOSTEncode(ByVal strPost As String) As String
            strPost = Replace(strPost, "\", "")
            strPost = System.Web.HttpUtility.UrlEncode(strPost)
            strPost = Replace(strPost, "%2f", "/")
            HTTPPOSTEncode = strPost
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Sets the ApplicationName for the MemberRole API
        ''' </summary>
        ''' <remarks>
        ''' This overload takes a the PortalId
        ''' </remarks>
        '''	<param name="PortalID">The Portal Id</param>
        ''' <history>
        '''		[cnurse]	01/18/2005	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub SetApplicationName(ByVal PortalID As Integer)
            HttpContext.Current.Items("ApplicationName") = GetApplicationName(PortalID)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Sets the ApplicationName for the MemberRole API
        ''' </summary>
        ''' <remarks>
        ''' This overload takes a the PortalId
        ''' </remarks>
        '''	<param name="ApplicationName">The Application Name to set</param>
        ''' <history>
        '''		[cnurse]	01/18/2005	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub SetApplicationName(ByVal ApplicationName As String)
            HttpContext.Current.Items("ApplicationName") = ApplicationName
        End Sub

#End Region

        ' format an address on a single line ( ie. Unit, Street, City, Region, Country, PostalCode )
        Public Function FormatAddress(ByVal Unit As Object, ByVal Street As Object, ByVal City As Object, ByVal Region As Object, ByVal Country As Object, ByVal PostalCode As Object) As String

            Dim strAddress As String = ""

            If Not Unit Is Nothing Then
                If Trim(Unit.ToString()) <> "" Then
                    strAddress += ", " & Unit.ToString
                End If
            End If
            If Not Street Is Nothing Then
                If Trim(Street.ToString()) <> "" Then
                    strAddress += ", " & Street.ToString
                End If
            End If
            If Not City Is Nothing Then
                If Trim(City.ToString()) <> "" Then
                    strAddress += ", " & City.ToString
                End If
            End If
            If Not Region Is Nothing Then
                If Trim(Region.ToString()) <> "" Then
                    strAddress += ", " & Region.ToString
                End If
            End If
            If Not Country Is Nothing Then
                If Trim(Country.ToString()) <> "" Then
                    strAddress += ", " & Country.ToString
                End If
            End If
            If Not PostalCode Is Nothing Then
                If Trim(PostalCode.ToString()) <> "" Then
                    strAddress += ", " & PostalCode.ToString
                End If
            End If
            If Trim(strAddress) <> "" Then
                strAddress = Mid(strAddress, 3)
            End If

            FormatAddress = strAddress

        End Function

        ' obfuscate sensitive data to prevent collection by robots and spiders and crawlers
        Public Function CloakText(ByVal PersonalInfo As String) As String

            If Not PersonalInfo Is Nothing Then
                Dim sb As New StringBuilder

                ' convert to ASCII character codes
                sb.Remove(0, sb.Length)
                Dim StringLength As Integer = PersonalInfo.Length - 1
                For i As Integer = 0 To StringLength
                    sb.Append(Asc(PersonalInfo.Substring(i, 1)).ToString)
                    If i < StringLength Then
                        sb.Append(",")
                    End If
                Next

                ' build script block
                Dim sbScript As New StringBuilder

                sbScript.Append(vbCrLf & "<script language=""javascript"">" & vbCrLf)
                sbScript.Append("<!-- " & vbCrLf)
                sbScript.Append("   document.write(String.fromCharCode(" & sb.ToString & "))" & vbCrLf)
                sbScript.Append("// -->" & vbCrLf)
                sbScript.Append("</script>" & vbCrLf)

                Return sbScript.ToString
            Else : Return Null.NullString
            End If

        End Function

        ' returns a SQL Server compatible date
        Public Function GetMediumDate(ByVal strDate As String) As String

            If strDate <> "" Then
                Dim datDate As Date = Convert.ToDateTime(strDate)

                Dim strYear As String = Year(datDate).ToString
                Dim strMonth As String = MonthName(Month(datDate), True)
                Dim strDay As String = Day(datDate).ToString

                strDate = strDay & "-" & strMonth & "-" & strYear
            End If

            Return strDate

        End Function

        ' returns a SQL Server compatible date
        Public Function GetShortDate(ByVal strDate As String) As String

            If strDate <> "" Then
                Dim datDate As Date = Convert.ToDateTime(strDate)

                Dim strYear As String = Year(datDate).ToString
                Dim strMonth As String = Month(datDate).ToString
                Dim strDay As String = Day(datDate).ToString

                strDate = strMonth & "/" & strDay & "/" & strYear
            End If

            Return strDate

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Returns whether the tab being displayed is in preview mode
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	9/16/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function IsTabPreview() As Boolean
            Return (PortalController.GetCurrentPortalSettings.UserMode = PortalSettings.Mode.View)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Returns whether the currnet tab is in LayoutMode
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	9/16/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function IsLayoutMode() As Boolean
            Dim blnReturn As Boolean = False
            Dim _portalSettings As PortalSettings = PortalController.GetCurrentPortalSettings
            If (PortalSecurity.IsInRole(_portalSettings.AdministratorRoleName.ToString) = True OrElse PortalSecurity.IsInRoles(_portalSettings.ActiveTab.AdministratorRoles.ToString) = True) AndAlso IsTabPreview() = False Then
                If _portalSettings.ActiveTab.IsAdminTab = False Then
                    blnReturn = True
                End If
            End If
            Return blnReturn
        End Function

        ' returns a boolean value whether the control is an admin control
        Public Function IsAdminControl() As Boolean

            ' This is needed to avoid an exception if there is no Context.  This will occur if code is called from the Scheduler
            If HttpContext.Current Is Nothing Then
                Return False
            End If
            Return (IsNothing(HttpContext.Current.Request.QueryString("mid")) = False) Or _
             (IsNothing(HttpContext.Current.Request.QueryString("ctl")) = False)

        End Function

        ' returns a boolean value whether the page should display an admin skin
        Public Function IsAdminSkin(ByVal IsAdminTab As Boolean) As Boolean

            Dim AdminKeys As String = "tab,module,importmodule,exportmodule,help"

            Dim ControlKey As String = ""
            If Not HttpContext.Current.Request.QueryString("ctl") Is Nothing Then
                ControlKey = HttpContext.Current.Request.QueryString("ctl").ToLower
            End If

            Dim ModuleID As Integer = -1
            If Not HttpContext.Current.Request.QueryString("mid") Is Nothing Then
                ModuleID = Integer.Parse(HttpContext.Current.Request.QueryString("mid"))
            End If

            Return IsAdminTab OrElse (ControlKey <> "" And ControlKey <> "view" And ModuleID <> -1) OrElse (ControlKey <> "" And AdminKeys.IndexOf(ControlKey) <> -1 And ModuleID = -1)

        End Function

        ' Deprecated PreventSQLInjection Function to consolidate Security Filter functions in the PortalSecurity class.
        Public Function PreventSQLInjection(ByVal strSQL As String) As String
            Return (New PortalSecurity).InputFilter(strSQL, PortalSecurity.FilterFlag.NoSQL)
        End Function

        ' creates RRS files
        Public Sub CreateRSS(ByVal dr As IDataReader, ByVal TitleField As String, ByVal URLField As String, ByVal CreatedDateField As String, ByVal SyndicateField As String, ByVal DomainName As String, ByVal FileName As String)

            ' Obtain PortalSettings from Current Context
            Dim _portalSettings As PortalSettings = PortalController.GetCurrentPortalSettings

            ' create RSS file
            Dim strRSS As String = ""

            Dim strRelativePath As String = DomainName & Replace(Mid(FileName, InStr(1, FileName, "\Portals")), "\", "/")
            strRelativePath = Left(strRelativePath, InStrRev(strRelativePath, "/"))

            While dr.Read()
                If Convert.ToBoolean(dr(SyndicateField)) Then
                    strRSS += "      <item>" & ControlChars.CrLf
                    strRSS += "         <title>" & dr(TitleField).ToString & "</title>" & ControlChars.CrLf
                    If InStr(1, dr("URL").ToString, "://") = 0 Then
                        If IsNumeric(dr("URL").ToString) Then
                            strRSS += "         <link>" & DomainName & "/" & glbDefaultPage & "?tabid=" & dr(URLField).ToString & "</link>" & ControlChars.CrLf
                        Else
                            strRSS += "         <link>" & strRelativePath & dr(URLField).ToString & "</link>" & ControlChars.CrLf
                        End If
                    Else
                        strRSS += "         <link>" & dr(URLField).ToString & "</link>" & ControlChars.CrLf
                    End If
                    strRSS += "         <description>" & _portalSettings.PortalName & " " & GetMediumDate(dr(CreatedDateField).ToString) & "</description>" & ControlChars.CrLf
                    strRSS += "     </item>" & ControlChars.CrLf
                End If
            End While
            dr.Close()

            If strRSS <> "" Then
                strRSS = "<?xml version=""1.0"" encoding=""iso-8859-1""?>" & ControlChars.CrLf & _
                 "<rss version=""0.91"">" & ControlChars.CrLf & _
                 "  <channel>" & ControlChars.CrLf & _
                 "     <title>" & _portalSettings.PortalName & "</title>" & ControlChars.CrLf & _
                 "     <link>" & DomainName & "</link>" & ControlChars.CrLf & _
                 "     <description>" & _portalSettings.PortalName & "</description>" & ControlChars.CrLf & _
                 "     <language>en-us</language>" & ControlChars.CrLf & _
                 "     <copyright>" & _portalSettings.FooterText & "</copyright>" & ControlChars.CrLf & _
                 "     <webMaster>" & _portalSettings.Email & "</webMaster>" & ControlChars.CrLf & _
                 strRSS & _
                 "   </channel>" & ControlChars.CrLf & _
                 "</rss>"

                Dim objStream As StreamWriter
                objStream = File.CreateText(FileName)
                objStream.WriteLine(strRSS)
                objStream.Close()
            Else
                If File.Exists(FileName) Then
                    File.Delete(FileName)
                End If
            End If

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' injects the upload directory into raw HTML for src and background tags
        ''' </summary>
        ''' <param name="strHTML">raw HTML text</param>
        ''' <param name="strUploadDirectory">path of portal image directory</param>
        ''' <returns>HTML with paths for images and background corrected</returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[sleupold]	8/18/2007	corrected and refactored
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function ManageUploadDirectory(ByVal strHTML As String, ByVal strUploadDirectory As String) As String
            strHTML = ManageTokenUploadDirectory(strHTML, strUploadDirectory, "src")
            Return ManageTokenUploadDirectory(strHTML, strUploadDirectory, "background")
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' injects the upload directory into raw HTML for a single token
        ''' </summary>
        ''' <param name="strHTML">raw HTML text</param>
        ''' <param name="strUploadDirectory">path of portal image directory</param>
        ''' <param name="strToken">token to be replaced</param>
        ''' <returns>HTML with paths for images and background corrected</returns>
        ''' <remarks>
        ''' called by ManageUploadDirectory for each token.
        ''' </remarks>
        ''' <history>
        ''' 	[sleupold]	8/18/2007	created as refactoring of ManageUploadDirectory
        '''                             added proper handling of subdirectory installations.
        '''     [sleupold] 11/03/2007   case insensitivity added
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function ManageTokenUploadDirectory(ByVal strHTML As String, ByVal strUploadDirectory As String, ByVal strToken As String) As String
            Dim P As Integer
            Dim R As Integer
            Dim S As Integer = 0
            Dim tLen As Integer
            Dim strURL As String
            Dim sbBuff As New StringBuilder("")

            If strHTML <> "" Then
                tLen = strToken.Length + 2
                Dim _UploadDirectory As String = strUploadDirectory.ToLower

                'find position of first occurrance:
                P = strHTML.IndexOf(strToken & "=""", StringComparison.InvariantCultureIgnoreCase)
                While P <> -1
                    sbBuff.Append(strHTML.Substring(S, P - S + tLen)) 'keep charactes left of URL
                    S = P + tLen 'save startpos of URL
                    R = strHTML.IndexOf("""", S) 'end of URL
                    If R >= 0 Then
                        strURL = strHTML.Substring(S, R - S).ToLower
                    Else
                        strURL = strHTML.Substring(S).ToLower
                    End If
                    ' add uploaddirectory if we are linking internally and the uploaddirectory is not already included
                    If Not strURL.Contains("://") AndAlso Not strURL.StartsWith("/") AndAlso Not strURL.StartsWith(_UploadDirectory) Then
                        sbBuff.Append(strUploadDirectory)
                    End If
                    'find position of next occurrance:
                    P = strHTML.IndexOf(strToken & "=""", S + Len(strURL) + 2, StringComparison.InvariantCultureIgnoreCase)
                End While

                If S > -1 Then sbBuff.Append(strHTML.Substring(S)) 'append characters of last URL and behind
            End If

            Return sbBuff.ToString
        End Function


        ' returns the database connection string
        <Obsolete("This function has been replaced by DotNetNuke.Common.Utilities.Config.GetConnectionString")> _
        Public Function GetDBConnectionString() As String

            Return Config.GetConnectionString()

        End Function

        ' uses recursion to search the control hierarchy for a specific control based on controlname
        Public Function FindControlRecursive(ByVal objControl As Control, ByVal strControlName As String) As Control
            If objControl.Parent Is Nothing Then
                Return Nothing
            Else
                If Not objControl.Parent.FindControl(strControlName) Is Nothing Then
                    Return objControl.Parent.FindControl(strControlName)
                Else
                    Return FindControlRecursive(objControl.Parent, strControlName)
                End If
            End If
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Searches control hierarchy from top down to find a control matching the passed in name
        ''' </summary>
        ''' <param name="objParent">Root control to begin searching</param>
        ''' <param name="strControlName">Name of control to look for</param>
        ''' <returns></returns>
        ''' <remarks>
        ''' This differs from FindControlRecursive in that it looks down the control hierarchy, whereas, the 
        ''' FindControlRecursive starts at the passed in control and walks the tree up.  Therefore, this function is 
        ''' more a expensive task.
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	9/17/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function FindControlRecursiveDown(ByVal objParent As Control, ByVal strControlName As String) As Control
            Dim objCtl As Control
            Dim objChild As Control
            objCtl = objParent.FindControl(strControlName)
            If objCtl Is Nothing Then
                For Each objChild In objParent.Controls
                    objCtl = FindControlRecursiveDown(objChild, strControlName)
                    If Not objCtl Is Nothing Then Exit For
                Next
            End If
            Return objCtl
        End Function

        'set focus to any control
        Public Sub SetFormFocus(ByVal control As Control)
            If Not control.Page Is Nothing And control.Visible Then
                If control.Page.Request.Browser.EcmaScriptVersion.Major >= 1 Then

                    'JH dnn.js mod
                    If DotNetNuke.UI.Utilities.ClientAPI.ClientAPIDisabled = False Then
                        DotNetNuke.UI.Utilities.ClientAPI.RegisterClientReference(control.Page, DotNetNuke.UI.Utilities.ClientAPI.ClientNamespaceReferences.dnn)
                        UI.Utilities.DNNClientAPI.AddBodyOnloadEventHandler(control.Page, "__dnn_SetInitialFocus('" & control.ClientID & "');")
                    Else
                        ' Create JavaScript 
                        Dim sb As New System.Text.StringBuilder
                        sb.Append("<SCRIPT LANGUAGE='JavaScript'>")
                        sb.Append("<!--")
                        sb.Append(ControlChars.Lf)
                        sb.Append("function SetInitialFocus() {")
                        sb.Append(ControlChars.Lf)
                        sb.Append(" document.")

                        ' Find the Form 
                        Dim objParent As Control = control.Parent
                        While Not TypeOf objParent Is System.Web.UI.HtmlControls.HtmlForm
                            objParent = objParent.Parent
                        End While
                        sb.Append(objParent.ClientID)
                        sb.Append("['")
                        sb.Append(control.UniqueID)
                        sb.Append("'].focus(); }")
                        sb.Append("window.onload = SetInitialFocus;")
                        sb.Append(ControlChars.Lf)
                        sb.Append("// -->")
                        sb.Append(ControlChars.Lf)
                        sb.Append("</SCRIPT>")

                        ' Register Client Script 
                        ClientAPI.RegisterClientScriptBlock(control.Page, "InitialFocus", sb.ToString())
                    End If
                End If
            End If
        End Sub

        Public Function GetExternalRequest(ByVal Address As String) As HttpWebRequest
            ' Obtain PortalSettings from Current Context
            Dim _portalSettings As PortalSettings = PortalController.GetCurrentPortalSettings

            ' Create the request object
            Dim objRequest As HttpWebRequest = CType(WebRequest.Create(Address), HttpWebRequest)

            ' Set a time out to the request ... 10 seconds
            If Not Integer.TryParse(CType(Common.Globals.HostSettings("WebRequestTimeout"), String), objRequest.Timeout) Then
                objRequest.Timeout = 10000
            End If

            ' Attach a User Agent to the request
            objRequest.UserAgent = "DotNetNuke"

            ' If there is Proxy info, apply it to the Request
            If CType(Common.Globals.HostSettings("ProxyServer"), String) <> "" Then

                ' Create a new Proxy
                Dim Proxy As WebProxy

                ' Create a new Network Credentials item
                Dim ProxyCredentials As NetworkCredential

                ' Fill Proxy info from host settings
                Proxy = New WebProxy(CType(Common.Globals.HostSettings("ProxyServer"), String), Convert.ToInt32(Common.Globals.HostSettings("ProxyPort")))

                If Not CType(Common.Globals.HostSettings("ProxyUsername"), String) = "" Then
                    ' Fill the credential info from host settings
                    ProxyCredentials = New NetworkCredential(CType(Common.Globals.HostSettings("ProxyUsername"), String), CType(Common.Globals.HostSettings("ProxyPassword"), String))

                    'Apply credentials to proxy
                    Proxy.Credentials = ProxyCredentials
                End If

                ' Apply Proxy to Request
                objRequest.Proxy = Proxy

            End If
            Return objRequest
        End Function

        Public Function GetExternalRequest(ByVal Address As String, ByVal Credentials As NetworkCredential) As HttpWebRequest
            ' Create the request object
            Dim objRequest As HttpWebRequest = CType(WebRequest.Create(Address), HttpWebRequest)

            ' Set a time out to the request ... 10 seconds
            If Not Integer.TryParse(CType(Common.Globals.HostSettings("WebRequestTimeout"), String), objRequest.Timeout) Then
                objRequest.Timeout = 10000
            End If

            ' Attach a User Agent to the request
            objRequest.UserAgent = "DotNetNuke"

            ' Attach supplied credentials
            If Not Credentials.UserName Is Nothing Then
                objRequest.Credentials = Credentials
            End If

            ' If there is Proxy info, apply it to the Request
            If CType(Common.Globals.HostSettings("ProxyServer"), String) <> "" Then

                ' Create a new Proxy
                Dim Proxy As WebProxy

                ' Create a new Network Credentials item
                Dim ProxyCredentials As NetworkCredential

                ' Fill Proxy info from host settings
                Proxy = New WebProxy(CType(Common.Globals.HostSettings("ProxyServer"), String), Convert.ToInt32(Common.Globals.HostSettings("ProxyPort")))

                If Not CType(Common.Globals.HostSettings("ProxyUsername"), String) = "" Then
                    ' Fill the credential info from host settings
                    ProxyCredentials = New NetworkCredential(CType(Common.Globals.HostSettings("ProxyUsername"), String), CType(Common.Globals.HostSettings("ProxyPassword"), String))

                    'Apply credentials to proxy
                    Proxy.Credentials = ProxyCredentials
                End If

                ' Apply Proxy to Request
                objRequest.Proxy = Proxy

            End If
            Return objRequest
        End Function

        Public Sub DeleteFolderRecursive(ByVal strRoot As String)
            If strRoot <> "" Then
                Dim strFolder As String
                If Directory.Exists(strRoot) Then
                    For Each strFolder In Directory.GetDirectories(strRoot)
                        DeleteFolderRecursive(strFolder)
                    Next
                    Dim strFile As String
                    For Each strFile In Directory.GetFiles(strRoot)
                        Try
                            File.SetAttributes(strFile, FileAttributes.Normal)
                            File.Delete(strFile)
                        Catch
                            ' error deleting file
                        End Try
                    Next
                    Try
                        Directory.Delete(strRoot)
                    Catch
                        ' error deleting folder
                    End Try
                End If
            End If
        End Sub

        Public Sub DeleteFilesRecursive(ByVal strRoot As String, ByVal filter As String)
            If strRoot <> "" Then
                Dim strFolder As String
                If Directory.Exists(strRoot) Then
                    For Each strFolder In Directory.GetDirectories(strRoot)
                        DeleteFilesRecursive(strFolder, filter)
                    Next

                    Dim strFile As String
                    For Each strFile In Directory.GetFiles(strRoot, "*" + filter)
                        Try
                            File.SetAttributes(strFile, FileAttributes.Normal)
                            File.Delete(strFile)
                        Catch
                            ' error deleting file
                        End Try
                    Next
                End If
            End If
        End Sub

        Public Function CreateValidID(ByVal strID As String) As String

            Dim strBadCharacters As String = " ./-\"

            Dim intIndex As Integer
            For intIndex = 0 To strBadCharacters.Length - 1
                strID = strID.Replace(strBadCharacters.Substring(intIndex, 1), "_")
            Next

            Return strID

        End Function

        Public Function CleanFileName(ByVal FileName As String) As String
            Return CleanFileName(FileName, "", "")
        End Function

        Public Function CleanFileName(ByVal FileName As String, ByVal BadChars As String) As String
            Return CleanFileName(FileName, BadChars, "")
        End Function

        Public Function CleanFileName(ByVal FileName As String, ByVal BadChars As String, ByVal ReplaceChar As String) As String

            Dim strFileName As String = FileName

            If BadChars = "" Then
                BadChars = ":/\?*|" & Chr(34) & Chr(39) & Chr(9)
            End If

            If ReplaceChar = "" Then
                ReplaceChar = "_"
            End If

            Dim intCounter As Integer

            For intCounter = 0 To Len(BadChars) - 1
                strFileName = strFileName.Replace(BadChars.Substring(intCounter, 1), ReplaceChar)
            Next intCounter

            Return strFileName

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' CleanName - removes characters from Module/Tab names that are being used for file names
        ''' in Module/Tab Import/Export.  
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <returns>A cleaned string</returns>
        ''' <history>
        ''' 	[cnurse]	10/11/2007	moved from Import/Export Module user controls to avoid 
        '''                             duplication and for use in new Import and Export Tab
        '''                             user controls
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function CleanName(ByVal Name As String) As String
            Dim strName As String = Name
            Dim strBadChars As String = ". ~`!@#$%^&*()-_+={[}]|\:;<,>?/" & Chr(34) & Chr(39)
            Dim intCounter As Integer
            For intCounter = 0 To Len(strBadChars) - 1
                strName = strName.Replace(strBadChars.Substring(intCounter, 1), "")
            Next intCounter
            Return strName
        End Function


#Region "Url Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Adds HTTP to URL if no other protocol specified
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="strURL">The url</param>
        ''' <returns>The formatted url</returns>
        ''' <history>
        '''		[cnurse]	12/16/2004	documented
        '''     [cnurse]    05/06/2005  added chack for mailto: protocol
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function AddHTTP(ByVal strURL As String) As String
            If strURL <> "" Then
                If InStr(1, strURL, "mailto:") = 0 And InStr(1, strURL, "://") = 0 And InStr(1, strURL, "~") = 0 And InStr(1, strURL, "\\") = 0 Then
                    If HttpContext.Current.Request.IsSecureConnection Then
                        strURL = "https://" & strURL
                    Else
                        strURL = "http://" & strURL
                    End If
                End If
            End If
            Return strURL
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Generates the Application root url (including the tab/page)
        ''' </summary>
        ''' <remarks>
        ''' This overload assumes the current page
        ''' </remarks>
        ''' <returns>The formatted root url</returns>
        ''' <history>
        '''		[cnurse]	12/16/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function ApplicationURL() As String

            ' Obtain PortalSettings from Current Context
            Dim _portalSettings As PortalSettings = PortalController.GetCurrentPortalSettings

            If Not _portalSettings Is Nothing Then
                Return (ApplicationURL(_portalSettings.ActiveTab.TabID))
            Else
                Return (ApplicationURL(-1))
            End If

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Generates the Application root url (including the tab/page)
        ''' </summary>
        ''' <remarks>
        ''' This overload takes the tabid (page id) as a parameter
        ''' </remarks>
        ''' <param name="TabID">The id of the tab/page</param>
        ''' <returns>The formatted root url</returns>
        ''' <history>
        '''		[cnurse]	12/16/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function ApplicationURL(ByVal TabID As Integer) As String

            Dim strURL As String = "~/" & glbDefaultPage

            If TabID <> -1 Then
                strURL += "?tabid=" & TabID.ToString
            End If

            Return strURL

        End Function

        Public Function ImportUrl(ByVal ModuleId As Integer, ByVal url As String) As String

            Dim strUrl As String = url

            If GetURLType(url) = TabType.File Then

                Dim objModuleController As New ModuleController
                Dim objFileController As New FileController
                Dim objModule As ModuleInfo = objModuleController.GetModule(ModuleId, Null.NullInteger, True)

                strUrl = "FileID=" + objFileController.ConvertFilePathToFileId(url, objModule.PortalID).ToString
            End If

            Return strUrl

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Generates the correctly formatted friendly url.
        ''' </summary>
        ''' <remarks>
        ''' Assumes Default.aspx, and that portalsettings are saved to Context
        ''' </remarks>
        ''' <param name="tab">The current tab</param>
        ''' <param name="path">The path to format.</param>
        ''' <returns>The formatted (friendly) url</returns>
        ''' <history>
        '''		[cnurse]	12/16/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function FriendlyUrl(ByVal tab As TabInfo, ByVal path As String) As String

            Return FriendlyUrl(tab, path, glbDefaultPage)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Generates the correctly formatted friendly url
        ''' </summary>
        ''' <remarks>
        ''' This overload includes an optional page to include in the url.
        ''' </remarks>
        ''' <param name="tab">The current tab</param>
        ''' <param name="path">The path to format.</param>
        ''' <param name="pageName">The page to include in the url.</param>
        ''' <returns>The formatted (friendly) url</returns>
        ''' <history>
        '''		[cnurse]	12/16/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function FriendlyUrl(ByVal tab As TabInfo, ByVal path As String, ByVal pageName As String) As String

            Dim _portalSettings As PortalSettings = PortalController.GetCurrentPortalSettings

            Return FriendlyUrl(tab, path, pageName, _portalSettings)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Generates the correctly formatted friendly url
        ''' </summary>
        ''' <remarks>
        ''' This overload includes the portal settings for the site
        ''' </remarks>
        ''' <param name="tab">The current tab</param>
        ''' <param name="path">The path to format.</param>
        ''' <param name="settings">The portal Settings</param>
        ''' <returns>The formatted (friendly) url</returns>
        ''' <history>
        '''		[cnurse]	12/16/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function FriendlyUrl(ByVal tab As TabInfo, ByVal path As String, ByVal settings As PortalSettings) As String

            Return FriendlyUrl(tab, path, glbDefaultPage, settings)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Generates the correctly formatted friendly url
        ''' </summary>
        ''' <remarks>
        ''' This overload includes an optional page to include in the url, and the portal 
        ''' settings for the site
        ''' </remarks>
        ''' <param name="tab">The current tab</param>
        ''' <param name="path">The path to format.</param>
        ''' <param name="pageName">The page to include in the url.</param>
        ''' <param name="settings">The portal Settings</param>
        ''' <returns>The formatted (friendly) url</returns>
        ''' <history>
        '''		[cnurse]	12/16/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function FriendlyUrl(ByVal tab As TabInfo, ByVal path As String, ByVal pageName As String, ByVal settings As PortalSettings) As String

            Return DotNetNuke.Services.Url.FriendlyUrl.FriendlyUrlProvider.Instance().FriendlyUrl(tab, path, pageName, settings)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Generates the correctly formatted friendly url
        ''' </summary>
        ''' <remarks>
        ''' This overload includes an optional page to include in the url, and the portal 
        ''' settings for the site
        ''' </remarks>
        ''' <param name="tab">The current tab</param>
        ''' <param name="path">The path to format.</param>
        ''' <param name="pageName">The page to include in the url.</param>
        ''' <param name="portalAlias">The portal Alias for the site</param>
        ''' <returns>The formatted (friendly) url</returns>
        ''' <history>
        '''		[cnurse]	12/16/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function FriendlyUrl(ByVal tab As TabInfo, ByVal path As String, ByVal pageName As String, ByVal portalAlias As String) As String

            Return DotNetNuke.Services.Url.FriendlyUrl.FriendlyUrlProvider.Instance().FriendlyUrl(tab, path, pageName, portalAlias)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Returns the type of URl (T=other tab, F=file, U=URL, N=normal)
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="URL">The url</param>
        ''' <returns>The url type</returns>
        ''' <history>
        '''		[cnurse]	12/16/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function GetURLType(ByVal URL As String) As TabType

            If URL = "" Then
                Return TabType.Normal      ' normal tab
            Else
                If URL.ToLower.StartsWith("mailto:") = False And URL.IndexOf("://") = -1 And URL.StartsWith("~") = False And URL.StartsWith("\\") = False And URL.StartsWith("/") = False Then
                    If IsNumeric(URL) Then
                        Return TabType.Tab       ' internal tab ( ie. 23 = tabid )
                    Else
                        If URL.ToLower.StartsWith("userid=") Then
                            Return TabType.Member     ' userid=
                        Else
                            Return TabType.File       ' internal file ( ie. folder/file.ext or fileid= )
                        End If
                    End If
                Else
                    Return TabType.Url      ' external url ( eg. http://www.domain.com )
                End If
            End If

        End Function

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public Function NavigateURL() As String

            Dim _portalSettings As PortalSettings = PortalController.GetCurrentPortalSettings

            Return NavigateURL(_portalSettings.ActiveTab.TabID, Null.NullString)

        End Function

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public Function NavigateURL(ByVal TabID As Integer) As String

            Return NavigateURL(TabID, Null.NullString)

        End Function

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public Function NavigateURL(ByVal TabID As Integer, ByVal IsSuperTab As Boolean) As String

            Dim _portalSettings As PortalSettings = PortalController.GetCurrentPortalSettings

            Return NavigateURL(TabID, IsSuperTab, _portalSettings, Null.NullString, "", Nothing)

        End Function

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public Function NavigateURL(ByVal ControlKey As String) As String
            If ControlKey = "Access Denied" Then
                Return AccessDeniedURL()
            Else
                Dim _portalSettings As PortalSettings = PortalController.GetCurrentPortalSettings
                Return NavigateURL(_portalSettings.ActiveTab.TabID, ControlKey)
            End If
        End Function

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public Function NavigateURL(ByVal ControlKey As String, ByVal ParamArray AdditionalParameters As String()) As String
            Dim _portalSettings As PortalSettings = PortalController.GetCurrentPortalSettings
            Return NavigateURL(_portalSettings.ActiveTab.TabID, ControlKey, AdditionalParameters)
        End Function

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public Function NavigateURL(ByVal TabID As Integer, ByVal ControlKey As String) As String

            Dim _portalSettings As PortalSettings = PortalController.GetCurrentPortalSettings

            Return NavigateURL(TabID, _portalSettings, ControlKey, Nothing)

        End Function

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public Function NavigateURL(ByVal TabID As Integer, ByVal ControlKey As String, ByVal ParamArray AdditionalParameters As String()) As String

            Dim _portalSettings As PortalSettings = PortalController.GetCurrentPortalSettings

            Return NavigateURL(TabID, _portalSettings, ControlKey, AdditionalParameters)

        End Function

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public Function NavigateURL(ByVal TabID As Integer, ByVal settings As PortalSettings, ByVal ControlKey As String, ByVal ParamArray AdditionalParameters As String()) As String

            Dim isSuperTab As Boolean = False
            If Not (settings Is Nothing) Then
                If settings.ActiveTab.IsSuperTab Then
                    isSuperTab = True
                End If
            End If

            Return NavigateURL(TabID, isSuperTab, settings, ControlKey, AdditionalParameters)

        End Function

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public Function NavigateURL(ByVal TabID As Integer, ByVal IsSuperTab As Boolean, ByVal settings As PortalSettings, ByVal ControlKey As String, ByVal ParamArray AdditionalParameters As String()) As String

            Return NavigateURL(TabID, IsSuperTab, settings, ControlKey, Thread.CurrentThread.CurrentCulture.Name, AdditionalParameters)

        End Function

        ''' <summary>
        ''' Returns a full internal url
        ''' </summary>
        ''' <param name="TabID">The tab id linking to</param>
        ''' <param name="IsSuperTab">Is the destination tab a host tab?</param>
        ''' <param name="settings">the Portalsettings</param>
        ''' <param name="ControlKey">an optional controlkey. If no controlkey is needed, pass "" </param>
        ''' <param name="Language">an optional language. If language is an empty string, the current active culture will be added in the url.</param>
        ''' <param name="AdditionalParameters">Any aditional querystring parameters. Use this format: "param1=value1", "param2=value2", ... , "paramN=valueN"</param>
        ''' <returns>Returns a full internal url</returns>
        ''' <remarks></remarks>
        Public Function NavigateURL(ByVal TabID As Integer, ByVal IsSuperTab As Boolean, ByVal settings As PortalSettings, ByVal ControlKey As String, ByVal Language As String, ByVal ParamArray AdditionalParameters As String()) As String

            Dim strURL As String

            If TabID = Null.NullInteger Then
                strURL = ApplicationURL()
            Else
                strURL = ApplicationURL(TabID)
            End If

            If ControlKey <> "" Then
                strURL += "&ctl=" & ControlKey
            End If

            If Not (AdditionalParameters Is Nothing) Then
                For Each parameter As String In AdditionalParameters
                    If Not String.IsNullOrEmpty(parameter) Then
                        strURL += "&" & parameter
                    End If
                Next
            End If

            If IsSuperTab Then
                strURL += "&portalid=" & settings.PortalId.ToString
            End If

            'only add language to url if more than one locale is enabled, and if admin did not turn it off
            If Localization.GetEnabledLocales.Count > 1 AndAlso Localization.UseLanguageInUrl Then
                If Language = "" Then
                    strURL += "&language=" & Thread.CurrentThread.CurrentCulture.Name
                Else
                    strURL += "&language=" & Language
                End If
            End If

            If DotNetNuke.Entities.Host.HostSettings.GetHostSetting("UseFriendlyUrls") = "Y" Then

                For Each objTab As TabInfo In settings.DesktopTabs
                    If objTab.TabID = TabID Then
                        Return FriendlyUrl(objTab, strURL, settings)
                    End If
                Next

                Return FriendlyUrl(Nothing, strURL, settings)

            Else
                Return ResolveUrl(strURL)
            End If

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Generates the correctly formatted url
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="url">The url to format.</param>
        ''' <returns>The formatted (resolved) url</returns>
        ''' <history>
        '''		[cnurse]	12/16/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function ResolveUrl(ByVal url As String) As String

            ' String is Empty, just return Url
            If (url.Length = 0) Then
                Return url
            End If

            ' String does not contain a ~, so just return Url
            If (url.StartsWith("~") = False) Then
                Return url
            End If

            ' There is just the ~ in the Url, return the appPath
            If (url.Length = 1) Then
                Return Common.Globals.ApplicationPath
            End If

            If (url.ToCharArray()(1) = "/" Or url.ToCharArray()(1) = "\") Then

                ' Url looks like ~/ or ~\
                If (Common.Globals.ApplicationPath.Length > 1) Then
                    Return Common.Globals.ApplicationPath + "/" & url.Substring(2)
                Else
                    Return "/" & url.Substring(2)
                End If

            Else

                ' Url look like ~something
                If (Common.Globals.ApplicationPath.Length > 1) Then
                    Return Common.Globals.ApplicationPath & "/" & url.Substring(1)
                Else
                    Return Common.Globals.ApplicationPath & url.Substring(1)
                End If

            End If

        End Function

        Public Function AccessDeniedURL() As String
            Return AccessDeniedURL("")
        End Function

        Public Function AccessDeniedURL(ByVal Message As String) As String

            Dim strURL As String = ""

            Dim _portalSettings As PortalSettings = PortalController.GetCurrentPortalSettings

            If HttpContext.Current.Request.IsAuthenticated Then
                If Message = "" Then
                    ' redirect to access denied page
                    strURL = NavigateURL(_portalSettings.ActiveTab.TabID, "Access Denied")
                Else
                    ' redirect to access denied page with custom message
                    strURL = NavigateURL(_portalSettings.ActiveTab.TabID, "Access Denied", "message=" & HttpUtility.UrlEncode(Message))
                End If
            Else
                If _portalSettings.LoginTabId <> -1 Then
                    ' redirect to portal login page specified
                    strURL = NavigateURL(_portalSettings.LoginTabId, "", "returnurl=" & HttpUtility.UrlEncode(HttpContext.Current.Request.RawUrl))
                Else
                    ' redirect to current page
                    strURL = NavigateURL(_portalSettings.ActiveTab.TabID, "Login", "returnurl=" & HttpUtility.UrlEncode(HttpContext.Current.Request.RawUrl))
                End If
            End If

            Return strURL

        End Function

#End Region

        Public Function QueryStringEncode(ByVal QueryString As String) As String

            QueryString = HttpUtility.UrlEncode(QueryString)

            Return QueryString

        End Function

        Public Function QueryStringDecode(ByVal QueryString As String) As String

            QueryString = HttpUtility.UrlDecode(QueryString)
            Dim fullPath As String
            Try
                fullPath = HttpContext.Current.Request.MapPath(QueryString, HttpContext.Current.Request.ApplicationPath, False)
            Catch ex As HttpException
                'attempted cross application mapping
                Throw New HttpException(404, "Not Found")
            End Try
            Dim strDoubleDecodeURL As String = HttpContext.Current.Server.UrlDecode(HttpContext.Current.Server.UrlDecode(QueryString))
            If QueryString.IndexOf("..") <> -1 Or strDoubleDecodeURL.IndexOf("..") <> -1 Then
                'attempted parent path traversal
                Throw New HttpException(404, "Not Found")
            End If
            Return QueryString
        End Function

        Public Function EncodeReservedCharacters(ByVal QueryString As String) As String

            QueryString = QueryString.Replace("$", "%24")
            QueryString = QueryString.Replace("&", "%26")
            QueryString = QueryString.Replace("+", "%2B")
            QueryString = QueryString.Replace(",", "%2C")
            QueryString = QueryString.Replace("/", "%2F")
            QueryString = QueryString.Replace(":", "%3A")
            QueryString = QueryString.Replace(";", "%3B")
            QueryString = QueryString.Replace("=", "%3D")
            QueryString = QueryString.Replace("?", "%3F")
            QueryString = QueryString.Replace("@", "%40")

            Return QueryString

        End Function

        Public Function DateToString(ByVal DateValue As DateTime) As String

            Try
                If Not Null.IsNull(DateValue) Then
                    Return DateValue.ToString("s")
                Else
                    Return Null.NullString
                End If
            Catch ex As Exception
                Return Null.NullString
            End Try
        End Function

        Public Function GetHashValue(ByVal HashObject As Object, ByVal DefaultValue As String) As String
            If Not HashObject Is Nothing Then
                If Convert.ToString(HashObject) <> "" Then
                    Return Convert.ToString(HashObject)
                Else
                    Return DefaultValue
                End If
            Else
                Return DefaultValue
            End If
        End Function

        Public Function LinkClick(ByVal Link As String, ByVal TabID As Integer, ByVal ModuleID As Integer) As String
            Return LinkClick(Link, TabID, ModuleID, True, "")
        End Function

        Public Function LinkClick(ByVal Link As String, ByVal TabID As Integer, ByVal ModuleID As Integer, ByVal TrackClicks As Boolean) As String
            Return LinkClick(Link, TabID, ModuleID, TrackClicks, "")
        End Function

        Public Function LinkClick(ByVal Link As String, ByVal TabID As Integer, ByVal ModuleID As Integer, ByVal TrackClicks As Boolean, ByVal ContentType As String) As String
            Return LinkClick(Link, TabID, ModuleID, TrackClicks, ContentType <> "")
        End Function

        Public Function LinkClick(ByVal Link As String, ByVal TabID As Integer, ByVal ModuleID As Integer, ByVal TrackClicks As Boolean, ByVal ForceDownload As Boolean) As String

            Dim strLink As String = ""

            Dim UrlType As TabType = GetURLType(Link)

            If TrackClicks = True Or ForceDownload = True Or UrlType = TabType.File Then
                ' format LinkClick wrapper
                If Link.ToLower.StartsWith("fileid=") Then
                    strLink = Common.Globals.ApplicationPath & "/LinkClick.aspx?fileticket=" & UrlUtils.EncryptParameter(UrlUtils.GetParameterValue(Link))
                End If
                If Link.ToLower.StartsWith("userid=") Then
                    strLink = Common.Globals.ApplicationPath & "/LinkClick.aspx?userticket=" & UrlUtils.EncryptParameter(UrlUtils.GetParameterValue(Link))
                End If
                If strLink = "" Then
                    strLink = Common.Globals.ApplicationPath & "/LinkClick.aspx?link=" & HttpUtility.UrlEncode(Link)
                End If

                ' tabid is required to identify the portal where the click originated
                If TabID <> Null.NullInteger Then
                    strLink += "&tabid=" & TabID.ToString
                End If

                ' moduleid is used to identify the module where the url is stored
                If ModuleID <> -1 Then
                    strLink += "&mid=" & ModuleID.ToString
                End If

                'only add language to url if more than one locale is enabled, and if admin did not turn it off
                If Localization.GetEnabledLocales.Count > 1 AndAlso Localization.UseLanguageInUrl Then
                    strLink += "&language=" & Thread.CurrentThread.CurrentCulture.Name
                End If

                ' force a download dialog
                If ForceDownload Then
                    strLink += "&forcedownload=true"
                End If
            Else
                Select Case UrlType
                    Case TabType.Tab
                        strLink = NavigateURL(Integer.Parse(Link))
                    Case TabType.Member
                        Dim _portalSettings As PortalSettings = PortalController.GetCurrentPortalSettings
                        strLink = NavigateURL(_portalSettings.ActiveTab.TabID, "ViewProfile", "userticket=" & UrlUtils.EncryptParameter(UrlUtils.GetParameterValue(Link)))
                    Case Else
                        strLink = Link
                End Select
            End If

            Return strLink

        End Function

        <Obsolete("This function has been obsoleted: Use Common.Globals.LinkClick() for proper handling of URLs")> _
      Public Function LinkClickURL(ByVal Link As String) As String
            Dim _portalSettings As PortalSettings = PortalController.GetCurrentPortalSettings
            Return LinkClick(Link, _portalSettings.ActiveTab.TabID, -1, False)
        End Function
        Public Function GetRoleName(ByVal RoleID As Integer) As String

            If Convert.ToString(RoleID) = glbRoleAllUsers Then
                Return "All Users"
            ElseIf Convert.ToString(RoleID) = glbRoleUnauthUser Then
                Return "Unauthenticated Users"
            End If

            Dim htRoles As Hashtable = Nothing
            If Not Common.Globals.PerformanceSetting = Common.Globals.PerformanceSettings.NoCaching Then
                htRoles = CType(DataCache.GetCache("GetRoles"), Hashtable)
            End If

            If htRoles Is Nothing Then
                Dim objRoleController As New RoleController
                Dim arrRoles As ArrayList
                arrRoles = objRoleController.GetRoles()
                htRoles = New Hashtable
                Dim i As Integer
                For i = 0 To arrRoles.Count - 1
                    Dim objRole As RoleInfo
                    objRole = CType(arrRoles(i), RoleInfo)
                    htRoles.Add(objRole.RoleID, objRole.RoleName)
                Next
                If Not Common.Globals.PerformanceSetting = Common.Globals.PerformanceSettings.NoCaching Then
                    DataCache.SetCache("GetRoles", htRoles)
                End If
            End If
            Return CType(htRoles(RoleID), String)
        End Function

        Public Function GetContent(ByVal Content As String, ByVal ContentType As String) As XmlNode
            Dim xmlDoc As New XmlDocument
            xmlDoc.LoadXml(Content)
            If ContentType = "" Then
                Return xmlDoc.DocumentElement
            Else
                Return xmlDoc.SelectSingleNode(ContentType)
            End If
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetDesktopModuleByName is a Utility function that retrieves the Desktop Module
        ''' defeined by the name provided.
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="name">The Name of the Module</param>
        ''' <returns>The Desktop Module</returns>
        ''' <history>
        '''		[cnurse]	4/22/2005	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function GetDesktopModuleByName(ByVal name As String) As DesktopModuleInfo

            Dim objDesktopModules As New DesktopModuleController

            'First attempt to retrieve the module based on the Module Name
            Dim objDesktopModule As DesktopModuleInfo = objDesktopModules.GetDesktopModuleByModuleName(name)

            If objDesktopModule Is Nothing Then
                'Next attempt to retrieve the module based on the Friendly Name
                objDesktopModule = objDesktopModules.GetDesktopModuleByFriendlyName(name)
            End If

            Return objDesktopModule

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GenerateTabPath generates the TabPath used in Friendly URLS
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="ParentId">The Id of the Parent Tab</param>
        ''' <param name="TabName">The Name of the current Tab</param>
        ''' <returns>The TabPath</returns>
        ''' <history>
        '''		[cnurse]	1/28/2005	documented
        '''                             modified to remove characters not allowed in urls
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function GenerateTabPath(ByVal ParentId As Integer, ByVal TabName As String) As String
            Dim strTabPath As String = ""
            Dim objTabs As New TabController
            Dim objTab As TabInfo
            Dim strTabName As String
            objTab = objTabs.GetTab(ParentId, Null.NullInteger, False)
            Do While Not objTab Is Nothing
                strTabName = HtmlUtils.StripNonWord(objTab.TabName, False)
                strTabPath = "//" & strTabName & strTabPath
                If Null.IsNull(objTab.ParentId) Then
                    objTab = Nothing
                Else
                    objTab = objTabs.GetTab(objTab.ParentId, objTab.PortalID, False)
                End If
            Loop

            strTabPath = strTabPath & "//" & HtmlUtils.StripNonWord(TabName, False)
            Return strTabPath

        End Function

        Public Function FormatHelpUrl(ByVal HelpUrl As String, ByVal objPortalSettings As PortalSettings, ByVal Name As String) As String

            Return FormatHelpUrl(HelpUrl, objPortalSettings, Name, "")

        End Function

        Public Function FormatHelpUrl(ByVal HelpUrl As String, ByVal objPortalSettings As PortalSettings, ByVal Name As String, ByVal Version As String) As String

            Dim strURL As String = HelpUrl

            If strURL.IndexOf("?") <> -1 Then
                strURL += "&helpculture="
            Else
                strURL += "?helpculture="
            End If

            If Thread.CurrentThread.CurrentCulture.ToString.ToLower <> "" Then
                strURL += Thread.CurrentThread.CurrentCulture.ToString.ToLower
            Else
                strURL += objPortalSettings.DefaultLanguage.ToLower
            End If

            If Name <> "" Then
                strURL += "&helpmodule=" & System.Web.HttpUtility.UrlEncode(Name)
            End If

            If Version <> "" Then
                strURL += "&helpversion=" & System.Web.HttpUtility.UrlEncode(Version)
            End If

            Return AddHTTP(strURL)

        End Function

        Public Function GetOnLineHelp(ByVal HelpUrl As String, ByVal moduleConfig As ModuleInfo) As String

            Dim isAdminModule As Boolean = moduleConfig.IsAdmin
            Dim showOnlineHelp As String = CType(HostSettings("EnableModuleOnLineHelp"), String)
            Dim ctlString As String = CType(HttpContext.Current.Request.QueryString("ctl"), String)

            If ((showOnlineHelp = "Y" And Not isAdminModule) Or (isAdminModule)) Then
                If (isAdminModule) Or (IsAdminControl() And ctlString = "Module") Or (IsAdminControl() And ctlString = "Tab") Then
                    Dim hostHelpUrl As String = CType(HostSettings("HelpURL"), String)
                    HelpUrl = hostHelpUrl
                End If
            Else
                HelpUrl = ""
            End If

            Return HelpUrl

        End Function

#Region "Serialization Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' DeserializeHashTableBase64 deserializes a Hashtable using Binary Formatting
        ''' </summary>
        ''' <remarks>
        ''' While this method of serializing is no longer supported (due to Medium Trust
        ''' issue, it is still required for upgrade purposes.
        ''' </remarks>
        ''' <param name="Source">The String Source to deserialize</param>
        ''' <returns>The deserialized Hashtable</returns>
        ''' <history>
        '''		[cnurse]	2/16/2005	moved to Globals
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function DeserializeHashTableBase64(ByVal Source As String) As Hashtable
            Dim objHashTable As Hashtable
            If Source <> "" Then
                Dim bits As Byte() = Convert.FromBase64String(Source)
                Dim mem As MemoryStream = New MemoryStream(bits)
                Dim bin As BinaryFormatter = New BinaryFormatter
                Try
                    objHashTable = CType(bin.Deserialize(mem), Hashtable)
                Catch ex As Exception
                    objHashTable = New Hashtable
                End Try
                mem.Close()
            Else
                objHashTable = New Hashtable
            End If
            Return objHashTable
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' DeserializeHashTableXml deserializes a Hashtable using Xml Serialization
        ''' </summary>
        ''' <remarks>
        ''' This is the preferred method of serialization under Medium Trust
        ''' </remarks>
        ''' <param name="Source">The String Source to deserialize</param>
        ''' <returns>The deserialized Hashtable</returns>
        ''' <history>
        '''		[cnurse]	2/16/2005	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function DeserializeHashTableXml(ByVal Source As String) As Hashtable
            Return XmlUtils.DeSerializeHashtable(Source, "profile")
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' SerializeHashTableBase64 serializes a Hashtable using Binary Formatting
        ''' </summary>
        ''' <remarks>
        ''' While this method of serializing is no longer supported (due to Medium Trust
        ''' issue, it is still required for upgrade purposes.
        ''' </remarks>
        ''' <param name="Source">The Hashtable to serialize</param>
        ''' <returns>The serialized String</returns>
        ''' <history>
        '''		[cnurse]	2/16/2005	moved to Globals
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function SerializeHashTableBase64(ByVal Source As Hashtable) As String
            Dim strString As String
            If Source.Count <> 0 Then
                Dim bin As BinaryFormatter = New BinaryFormatter
                Dim mem As MemoryStream = New MemoryStream
                Try
                    bin.Serialize(mem, Source)
                    strString = Convert.ToBase64String(mem.GetBuffer(), 0, Convert.ToInt32(mem.Length))
                Catch ex As Exception
                    strString = ""
                Finally
                    mem.Close()
                End Try
            Else
                strString = ""
            End If
            Return strString
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' SerializeHashTableXml serializes a Hashtable using Xml Serialization
        ''' </summary>
        ''' <remarks>
        ''' This is the preferred method of serialization under Medium Trust
        ''' </remarks>
        ''' <param name="Source">The Hashtable to serialize</param>
        ''' <returns>The serialized String</returns>
        ''' <history>
        '''		[cnurse]	2/16/2005	moved to Globals
        '''     [cnurse]    01/19/2007  extracted to XmlUtils
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function SerializeHashTableXml(ByVal Source As Hashtable) As String
            Return XmlUtils.SerializeDictionary(Source, "profile")
        End Function


#End Region

#Region "Obsolete Functions retained for Binary Compatability"

        <Obsolete("This method has been deprecated.")> _
        Public Sub AddFile(ByVal strFileName As String, ByVal strExtension As String, ByVal FolderPath As String, ByVal strContentType As String, ByVal Length As Integer, ByVal imageWidth As Integer, ByVal imageHeight As Integer)

            ' Obtain PortalSettings from Current Context
            Dim _portalSettings As PortalSettings = PortalController.GetCurrentPortalSettings
            Dim PortalId As Integer = FileSystemUtils.GetFolderPortalId(_portalSettings)
            Dim objFiles As New FileController
            Dim objFolders As New FolderController
            Dim objFolder As FolderInfo = objFolders.GetFolder(PortalId, FolderPath, False)
            If Not objFolder Is Nothing Then
                objFiles.AddFile(PortalId, strFileName, strExtension, Length, imageWidth, imageHeight, strContentType, FolderPath, objFolder.FolderID, True)
            End If
        End Sub

        <Obsolete("This method has been deprecated. ")> _
        Public Function GetFileList(ByVal CurrentDirectory As DirectoryInfo, Optional ByVal strExtensions As String = "", Optional ByVal NoneSpecified As Boolean = True) As ArrayList
            Dim arrFileList As New ArrayList
            Dim strExtension As String = ""

            If NoneSpecified Then
                arrFileList.Add(New FileItem("", "<" + Services.Localization.Localization.GetString("None_Specified") + ">"))
            End If

            Dim File As String
            Dim Files As String() = Directory.GetFiles(CurrentDirectory.FullName)
            For Each File In Files
                If Convert.ToBoolean(InStr(1, File, ".")) Then
                    strExtension = Mid(File, InStrRev(File, ".") + 1)
                End If
                Dim FileName As String = File.Substring(CurrentDirectory.FullName.Length)
                If InStr(1, strExtensions.ToUpper, strExtension.ToUpper) <> 0 Or strExtensions = "" Then
                    arrFileList.Add(New FileItem(FileName, FileName))
                End If
            Next

            GetFileList = arrFileList
        End Function

        <Obsolete("This method has been deprecated. Replaced by GetSubFolderPath(ByVal strFileNamePath As String, ByVal portaId as Integer).")> _
        Public Function GetSubFolderPath(ByVal strFileNamePath As String) As String
            ' Obtain PortalSettings from Current Context
            Dim _portalSettings As PortalSettings = PortalController.GetCurrentPortalSettings
            Dim ParentFolderName As String
            If _portalSettings.ActiveTab.ParentId = _portalSettings.SuperTabId Then
                ParentFolderName = Common.Globals.HostMapPath.Replace("/", "\")
            Else
                ParentFolderName = _portalSettings.HomeDirectoryMapPath.Replace("/", "\")
            End If
            Dim strFolderpath As String = strFileNamePath.Substring(0, strFileNamePath.LastIndexOf("\") + 1)

            GetSubFolderPath = strFolderpath.Substring(ParentFolderName.Length).Replace("\", "/")

        End Function


        <Obsolete("This method has been deprecated. Replaced by same method in FileSystemUtils class.")> _
        Public Function UploadFile(ByVal RootPath As String, ByVal objHtmlInputFile As HttpPostedFile, Optional ByVal Unzip As Boolean = False) As String

            Return FileSystemUtils.UploadFile(RootPath, objHtmlInputFile, Unzip)

        End Function

#Region "Html functions moved to HtmlUtils.vb"

        <Obsolete("This function has been replaced by DotNetNuke.Common.Utilities.HtmlUtils.FormatEmail")> _
        Public Function FormatEmail(ByVal Email As String) As String
            Return HtmlUtils.FormatEmail(Email)
        End Function

        <Obsolete("This function has been replaced by DotNetNuke.Common.Utilities.HtmlUtils.FormatWebsite")> _
        Public Function FormatWebsite(ByVal Website As Object) As String
            Return HtmlUtils.FormatWebsite(Website)
        End Function

#End Region

#Region "Mail functions moved to Mail.vb"

        'These functions have been replaced by the Mail class in
        'DotNetNuke.Services.Mail.  They have been retained here
        'for backwards compatabily, but flagged as obsolete to
        'discourage use

        <Obsolete("This function has been replaced by DotNetNuke.Services.Mail.Mail.SendMail")> _
        Public Function SendNotification(ByVal MailFrom As String, ByVal MailTo As String, ByVal Bcc As String, ByVal Subject As String, ByVal Body As String) As String
            Return DotNetNuke.Services.Mail.Mail.SendMail(MailFrom, MailTo, Bcc, Subject, Body, _
                "", "", "", "", "", "")
        End Function

        <Obsolete("This function has been replaced by DotNetNuke.Services.Mail.Mail.SendMail")> _
        Public Function SendNotification(ByVal MailFrom As String, ByVal MailTo As String, ByVal Bcc As String, ByVal Subject As String, ByVal Body As String, ByVal Attachment As String) As String
            Return DotNetNuke.Services.Mail.Mail.SendMail(MailFrom, MailTo, Bcc, Subject, Body, _
                Attachment, "", "", "", "", "")
        End Function

        <Obsolete("This function has been replaced by DotNetNuke.Services.Mail.Mail.SendMail")> _
        Public Function SendNotification(ByVal MailFrom As String, ByVal MailTo As String, ByVal Bcc As String, ByVal Subject As String, ByVal Body As String, ByVal Attachment As String, ByVal BodyType As String) As String
            Return DotNetNuke.Services.Mail.Mail.SendMail(MailFrom, MailTo, Bcc, Subject, Body, _
                Attachment, BodyType, "", "", "", "")
        End Function

        <Obsolete("This function has been replaced by DotNetNuke.Services.Mail.Mail.SendMail")> _
        Public Function SendNotification(ByVal MailFrom As String, ByVal MailTo As String, ByVal Bcc As String, ByVal Subject As String, ByVal Body As String, ByVal Attachment As String, ByVal BodyType As String, ByVal SMTPServer As String, ByVal SMTPAuthentication As String, ByVal SMTPUsername As String, ByVal SMTPPassword As String) As String
            Return DotNetNuke.Services.Mail.Mail.SendMail(MailFrom, MailTo, Bcc, Subject, Body, _
                Attachment, BodyType, SMTPServer, SMTPAuthentication, SMTPUsername, SMTPPassword)
        End Function

        <Obsolete("This function has been replaced by DotNetNuke.Services.Mail.Mail.SendMail")> _
        Public Function SendNotification(ByVal MailFrom As String, ByVal MailTo As String, ByVal Cc As String, ByVal Bcc As String, ByVal Priority As DotNetNuke.Services.Mail.MailPriority, ByVal Subject As String, ByVal BodyFormat As DotNetNuke.Services.Mail.MailFormat, ByVal BodyEncoding As System.Text.Encoding, ByVal Body As String, ByVal Attachment As String, ByVal SMTPServer As String, ByVal SMTPAuthentication As String, ByVal SMTPUsername As String, ByVal SMTPPassword As String) As String
            Return DotNetNuke.Services.Mail.Mail.SendMail(MailFrom, MailTo, Cc, Bcc, Priority, Subject, _
              BodyFormat, BodyEncoding, Body, Attachment, SMTPServer, SMTPAuthentication, _
              SMTPUsername, SMTPPassword)
        End Function

        <Obsolete("This function has been replaced by DotNetNuke.Services.Mail.Mail.SendMail")> _
        Public Function SendMail(ByVal MailFrom As String, ByVal MailTo As String, ByVal Cc As String, ByVal Bcc As String, ByVal Priority As DotNetNuke.Services.Mail.MailPriority, ByVal Subject As String, ByVal BodyFormat As DotNetNuke.Services.Mail.MailFormat, ByVal BodyEncoding As System.Text.Encoding, ByVal Body As String, ByVal Attachment As String, ByVal SMTPServer As String, ByVal SMTPAuthentication As String, ByVal SMTPUsername As String, ByVal SMTPPassword As String) As String

            Return DotNetNuke.Services.Mail.Mail.SendMail(MailFrom, MailTo, Cc, Bcc, Priority, Subject, _
              BodyFormat, BodyEncoding, Body, Attachment, SMTPServer, SMTPAuthentication, _
              SMTPUsername, SMTPPassword)

        End Function

#End Region

#Region "Xml functions moved to XmlUtils.vb"

        <Obsolete("This function has been replaced by DotNetNuke.Common.Utilities.XmlUtils.XMLEncode")> _
        Public Function XMLEncode(ByVal HTML As String) As String
            Return XmlUtils.XMLEncode(HTML)
        End Function

#End Region

#End Region

    End Module

End Namespace
