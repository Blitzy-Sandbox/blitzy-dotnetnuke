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
Imports System.Configuration
Imports System.Data
Imports System.Globalization
Imports System.Xml.Serialization

Imports DotNetNuke.Entities.Portals
Imports DotNetNuke.Services.Tokens

Namespace DotNetNuke.Entities.Tabs

    Public Enum TabType
        File
        Normal
        Tab
        Url
        Member
    End Enum

    <XmlRoot("tab", IsNullable:=False)> Public Class TabInfo
        Implements IPropertyAccess

#Region "Private Members"

        Private _TabID As Integer
        Private _TabOrder As Integer
        Private _PortalID As Integer
        Private _TabName As String
        Private _AuthorizedRoles As String
        Private _IsVisible As Boolean
        Private _ParentId As Integer
        Private _Level As Integer
        Private _IconFile As String
        Private _AdministratorRoles As String
        Private _DisableLink As Boolean
        Private _Title As String
        Private _Description As String
        Private _KeyWords As String
        Private _IsDeleted As Boolean
        Private _Url As String
        Private _SkinSrc As String
        Private _ContainerSrc As String
        Private _TabPath As String
        Private _StartDate As Date
        Private _EndDate As Date
        Private _TabPermissions As Security.Permissions.TabPermissionCollection
        Private _HasChildren As Boolean
        Private _RefreshInterval As Integer
        Private _PageHeadText As String
        Private _IsSecure As Boolean

        Private _SuperTabIdSet As Boolean = Null.NullBoolean

        ' properties loaded in PortalSettings
        Private _SkinPath As String
        Private _ContainerPath As String
        Private _BreadCrumbs As ArrayList
        Private _Panes As ArrayList
        Private _Modules As ArrayList
        Private _IsSuperTab As Boolean

#End Region

#Region "Constructors"

        Public Sub New()
            'initialize the properties that
            'can be null in the database
            _PortalID = Null.NullInteger
            _AuthorizedRoles = Null.NullString
            _ParentId = Null.NullInteger
            _IconFile = Null.NullString
            _AdministratorRoles = Null.NullString
            _Title = Null.NullString
            _Description = Null.NullString
            _KeyWords = Null.NullString
            _Url = Null.NullString
            _SkinSrc = Null.NullString
            _ContainerSrc = Null.NullString
            _TabPath = Null.NullString
            _StartDate = Null.NullDate
            _EndDate = Null.NullDate
            _RefreshInterval = Null.NullInteger
            _PageHeadText = Null.NullString
        End Sub

#End Region

#Region "Public Properties"

        <XmlElement("tabid")> Public Property TabID() As Integer
            Get
                Return _TabID
            End Get
            Set(ByVal Value As Integer)
                _TabID = Value
            End Set
        End Property

        <XmlElement("taborder")> Public Property TabOrder() As Integer
            Get
                Return _TabOrder
            End Get
            Set(ByVal Value As Integer)
                _TabOrder = Value
            End Set
        End Property

        <XmlElement("portalid")> Public Property PortalID() As Integer
            Get
                Return _PortalID
            End Get
            Set(ByVal Value As Integer)
                _PortalID = Value
            End Set
        End Property

        <XmlElement("name")> Public Property TabName() As String
            Get
                Return _TabName
            End Get
            Set(ByVal Value As String)
                _TabName = Value
            End Set
        End Property

        <XmlElement("visible")> Public Property IsVisible() As Boolean
            Get
                Return _IsVisible
            End Get
            Set(ByVal Value As Boolean)
                _IsVisible = Value
            End Set
        End Property

        <XmlElement("parentid")> Public Property ParentId() As Integer
            Get
                Return _ParentId
            End Get
            Set(ByVal Value As Integer)
                _ParentId = Value
            End Set
        End Property

        <XmlIgnore()> Public Property Level() As Integer
            Get
                Return _Level
            End Get
            Set(ByVal Value As Integer)
                _Level = Value
            End Set
        End Property

        <XmlElement("iconfile")> Public Property IconFile() As String
            Get
                Return _IconFile
            End Get
            Set(ByVal Value As String)
                _IconFile = Value
            End Set
        End Property

        <XmlElement("disabled")> Public Property DisableLink() As Boolean
            Get
                Return _DisableLink
            End Get
            Set(ByVal Value As Boolean)
                _DisableLink = Value
            End Set
        End Property

        <XmlElement("title")> Public Property Title() As String
            Get
                Return _Title
            End Get
            Set(ByVal Value As String)
                _Title = Value
            End Set
        End Property

        <XmlElement("description")> Public Property Description() As String
            Get
                Return _Description
            End Get
            Set(ByVal Value As String)
                _Description = Value
            End Set
        End Property

        <XmlElement("keywords")> Public Property KeyWords() As String
            Get
                Return _KeyWords
            End Get
            Set(ByVal Value As String)
                _KeyWords = Value
            End Set
        End Property

        <XmlElement("isdeleted")> Public Property IsDeleted() As Boolean
            Get
                Return _IsDeleted
            End Get
            Set(ByVal Value As Boolean)
                _IsDeleted = Value
            End Set
        End Property

        <XmlElement("url")> Public Property Url() As String
            Get
                Return _Url
            End Get
            Set(ByVal Value As String)
                _Url = Value
            End Set
        End Property

        <XmlElement("skinsrc")> Public Property SkinSrc() As String
            Get
                Return _SkinSrc
            End Get
            Set(ByVal Value As String)
                _SkinSrc = Value
            End Set
        End Property

        <XmlElement("containersrc")> Public Property ContainerSrc() As String
            Get
                Return _ContainerSrc
            End Get
            Set(ByVal Value As String)
                _ContainerSrc = Value
            End Set
        End Property

        <XmlElement("tabpath")> Public Property TabPath() As String
            Get
                Return _TabPath
            End Get
            Set(ByVal Value As String)
                _TabPath = Value
            End Set
        End Property

        <XmlElement("startdate")> Public Property StartDate() As Date
            Get
                Return _StartDate
            End Get
            Set(ByVal Value As Date)
                _StartDate = Value
            End Set
        End Property

        <XmlElement("enddate")> Public Property EndDate() As Date
            Get
                Return _EndDate
            End Get
            Set(ByVal Value As Date)
                _EndDate = Value
            End Set
        End Property

        <XmlArray("tabpermissions"), XmlArrayItem("permission")> Public Property TabPermissions() As Security.Permissions.TabPermissionCollection
            Get
                Return _TabPermissions
            End Get
            Set(ByVal Value As Security.Permissions.TabPermissionCollection)
                _TabPermissions = Value
            End Set
        End Property

        <XmlElement("haschildren")> Public Property HasChildren() As Boolean
            Get
                Return _HasChildren
            End Get
            Set(ByVal Value As Boolean)
                _HasChildren = Value
            End Set
        End Property

        <XmlElement("refreshinterval")> Public Property RefreshInterval() As Integer
            Get
                Return _RefreshInterval
            End Get
            Set(ByVal Value As Integer)
                _RefreshInterval = Value
            End Set
        End Property

        <XmlElement("pageheadtext")> Public Property PageHeadText() As String
            Get
                Return _PageHeadText
            End Get
            Set(ByVal Value As String)
                _PageHeadText = Value
            End Set
        End Property

        <XmlElement("issecure")> Public Property IsSecure() As Boolean
            Get
                Return _IsSecure
            End Get
            Set(ByVal Value As Boolean)
                _IsSecure = Value
            End Set
        End Property

        <XmlElement("authorizedroles")> Public Property AuthorizedRoles() As String
            Get
                Return _AuthorizedRoles
            End Get
            Set(ByVal Value As String)
                _AuthorizedRoles = Value
            End Set
        End Property

        <XmlElement("administratorroles")> Public Property AdministratorRoles() As String
            Get
                Return _AdministratorRoles
            End Get
            Set(ByVal Value As String)
                _AdministratorRoles = Value
            End Set
        End Property


        ' properties loaded in PortalSettings
        <XmlIgnore()> Public Property SkinPath() As String
            Get
                Return _SkinPath
            End Get
            Set(ByVal Value As String)
                _SkinPath = Value
            End Set
        End Property

        <XmlIgnore()> Public Property ContainerPath() As String
            Get
                Return _ContainerPath
            End Get
            Set(ByVal Value As String)
                _ContainerPath = Value
            End Set
        End Property

        <XmlIgnore()> Public Property BreadCrumbs() As ArrayList
            Get
                Return _BreadCrumbs
            End Get
            Set(ByVal Value As ArrayList)
                _BreadCrumbs = Value
            End Set
        End Property

        <XmlIgnore()> Public Property Panes() As ArrayList
            Get
                Return _Panes
            End Get
            Set(ByVal Value As ArrayList)
                _Panes = Value
            End Set
        End Property

        <XmlIgnore()> Public Property Modules() As ArrayList
            Get
                Return _Modules
            End Get
            Set(ByVal Value As ArrayList)
                _Modules = Value
            End Set
        End Property

        <XmlIgnore()> Public Property IsSuperTab() As Boolean
            Get
                If _SuperTabIdSet Then
                    Return _IsSuperTab
                Else
                    Return (PortalID = Null.NullInteger)
                End If
            End Get
            Set(ByVal Value As Boolean)
                _IsSuperTab = Value
                _SuperTabIdSet = True
            End Set
        End Property

        <XmlIgnore()> Public ReadOnly Property TabType() As TabType
            Get
                Return GetURLType(_Url)
            End Get
        End Property

        <XmlIgnore()> Public ReadOnly Property FullUrl() As String
            Get
                Dim strUrl As String = ""

                Select Case TabType
                    Case TabType.Normal
                        ' normal tab
                        strUrl = NavigateURL(TabID, IsSuperTab)
                    Case TabType.Tab
                        ' alternate tab url
                        strUrl = NavigateURL(Convert.ToInt32(_Url))
                    Case TabType.File
                        ' file url
                        strUrl = LinkClick(_Url, TabID, Null.NullInteger)
                    Case TabType.Url
                        ' external url
                        strUrl = _Url
                End Select

                Return strUrl
            End Get
        End Property

        Public ReadOnly Property IsAdminTab() As Boolean
            Get
                If IsSuperTab Then
                    'Host Tab
                    Return True
                Else
                    'Portal Tab

                    'Get Portal Settings
                    Dim settings As PortalSettings = PortalController.GetCurrentPortalSettings

                    If settings Is Nothing Then
                        'If there are no settings try the PortalSettings Cache as this property is
                        'used during the creation of the PortalSettings
                        Dim objPortal As PortalInfo = CType(DataCache.GetPersistentCacheItem("GetPortalSettings" & PortalID.ToString, GetType(PortalInfo)), PortalInfo)

                        If objPortal Is Nothing Then
                            'If there is no portal in the Cache Get PortalInfo object from DB
                            Dim objPortalController As New PortalController
                            objPortal = objPortalController.GetPortal(PortalID)
                        End If

                        Return (TabID = objPortal.AdminTabId) Or (ParentId = objPortal.AdminTabId)
                    Else
                        Return (TabID = settings.AdminTabId) Or (ParentId = settings.AdminTabId)
                    End If

                End If
            End Get
        End Property

#End Region

#Region "Public Methods"

        Public Function Clone() As TabInfo

            ' create the object
            Dim objTabInfo As New TabInfo

            ' assign the property values
            objTabInfo.TabID = Me.TabID
            objTabInfo.TabOrder = Me.TabOrder
            objTabInfo.PortalID = Me.PortalID
            objTabInfo.TabName = Me.TabName
            objTabInfo.AuthorizedRoles = Me.AuthorizedRoles
            objTabInfo.IsVisible = Me.IsVisible
            objTabInfo.ParentId = Me.ParentId
            objTabInfo.Level = Me.Level
            objTabInfo.IconFile = Me.IconFile
            objTabInfo.AdministratorRoles = Me.AdministratorRoles
            objTabInfo.DisableLink = Me.DisableLink
            objTabInfo.Title = Me.Title
            objTabInfo.Description = Me.Description
            objTabInfo.KeyWords = Me.KeyWords
            objTabInfo.IsDeleted = Me.IsDeleted
            objTabInfo.Url = Me.Url
            objTabInfo.SkinSrc = Me.SkinSrc
            objTabInfo.ContainerSrc = Me.ContainerSrc
            objTabInfo.TabPath = Me.TabPath
            objTabInfo.StartDate = Me.StartDate
            objTabInfo.EndDate = Me.EndDate
            objTabInfo.TabPermissions = Me.TabPermissions
            objTabInfo.HasChildren = Me.HasChildren
            objTabInfo.SkinPath = Me.SkinPath
            objTabInfo.ContainerPath = Me.ContainerPath
            objTabInfo.IsSuperTab = Me.IsSuperTab
            objTabInfo.RefreshInterval = Me.RefreshInterval
            objTabInfo.PageHeadText = Me.PageHeadText
            objTabInfo.IsSecure = Me.IsSecure
            If Not Me.BreadCrumbs Is Nothing Then
                objTabInfo.BreadCrumbs = New ArrayList
                For Each t As TabInfo In Me.BreadCrumbs
                    objTabInfo.BreadCrumbs.Add(t.Clone())
                Next
            End If

            ' initialize collections which are populated later
            objTabInfo.Panes = New ArrayList
            objTabInfo.Modules = New ArrayList

            Return objTabInfo

        End Function

#End Region

#Region "IPropertyAccess Implementation"
   
        Public Function GetProperty(ByVal strPropertyName As String, ByVal strFormat As String, ByVal formatProvider As System.Globalization.CultureInfo, ByVal AccessingUser As UserInfo, ByVal CurrentScope As Scope, ByRef PropertyNotFound As Boolean) As String Implements IPropertyAccess.GetProperty
            Dim OutputFormat As String = String.Empty
            If strFormat = String.Empty Then OutputFormat = "g"
            Dim lowerPropertyName As String = strPropertyName.ToLower

            'Content locked for NoSettings
            If CurrentScope = Scope.NoSettings Then PropertyNotFound = True : Return PropertyAccess.ContentLocked

            PropertyNotFound = True
            Dim result As String = String.Empty
            Dim PublicProperty As Boolean = True

            Select Case lowerPropertyName
                Case "tabid"
                    PublicProperty = True : PropertyNotFound = False : result = (Me.TabID.ToString(OutputFormat, formatProvider))
                Case "taborder"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.TabOrder.ToString(OutputFormat, formatProvider))
                Case "portalid"
                    PublicProperty = True : PropertyNotFound = False : result = (Me.PortalID.ToString(OutputFormat, formatProvider))
                Case "tabname"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.TabName, strFormat)
                Case "isvisible"
                    PublicProperty = False : PropertyNotFound = False : result = (PropertyAccess.Boolean2LocalizedYesNo(Me.IsVisible, formatProvider))
                Case "parentid"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.ParentId.ToString(OutputFormat, formatProvider))
                Case "level"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.Level.ToString(OutputFormat, formatProvider))
                Case "iconfile"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.IconFile, strFormat)
                Case "disablelink"
                    PublicProperty = False : PropertyNotFound = False : result = (PropertyAccess.Boolean2LocalizedYesNo(Me.DisableLink, formatProvider))
                Case "title"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.Title, strFormat)
                Case "description"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.Description, strFormat)
                Case "keywords"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.KeyWords, strFormat)
                Case "isdeleted"
                    PublicProperty = False : PropertyNotFound = False : result = (PropertyAccess.Boolean2LocalizedYesNo(Me.IsDeleted, formatProvider))
                Case "url"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.Url, strFormat)
                Case "skinsrc"
                    PublicProperty = False : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.SkinSrc, strFormat)
                Case "containersrc"
                    PublicProperty = False : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.ContainerSrc, strFormat)
                Case "tabpath"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.TabPath, strFormat)
                Case "startdate"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.StartDate.ToString(OutputFormat, formatProvider))
                Case "enddate"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.EndDate.ToString(OutputFormat, formatProvider))
                Case "haschildren"
                    PublicProperty = False : PropertyNotFound = False : result = (PropertyAccess.Boolean2LocalizedYesNo(Me.HasChildren, formatProvider))
                Case "refreshinterval"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.RefreshInterval.ToString(OutputFormat, formatProvider))
                Case "pageheadtext"
                    PublicProperty = False : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.PageHeadText, strFormat)
                Case "authorizedroles"
                    PublicProperty = False : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.AuthorizedRoles, strFormat)
                Case "administratorroles"
                    PublicProperty = False : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.AdministratorRoles, strFormat)
                Case "skinpath"
                    PublicProperty = False : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.SkinPath, strFormat)
                Case "containerpath"
                    PublicProperty = False : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.ContainerPath, strFormat)
                Case "issupertab"
                    PublicProperty = False : PropertyNotFound = False : result = (PropertyAccess.Boolean2LocalizedYesNo(Me.IsSuperTab, formatProvider))
                Case "fullurl"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.FullUrl, strFormat)
                Case "isadmintab"
                    PublicProperty = False : PropertyNotFound = False : result = (PropertyAccess.Boolean2LocalizedYesNo(Me.IsAdminTab, formatProvider))
            End Select

            If Not PublicProperty And CurrentScope <> Scope.Debug Then
                PropertyNotFound = True
                result = PropertyAccess.ContentLocked
            End If

            Return result
        End Function

        Public ReadOnly Property Cacheability() As CacheLevel Implements Services.Tokens.IPropertyAccess.Cacheability
            Get
                Return CacheLevel.fullyCacheable
            End Get
        End Property

#End Region

    End Class

End Namespace

