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
Imports System.Runtime.Serialization

Namespace DotNetNuke.Entities.Modules
    Public Class ModuleSettingsBase
        Inherits PortalModuleBase

        Private _moduleSettings As Hashtable
        Private _tabModuleSettings As Hashtable
        Private _settings As Hashtable

        Public Shadows Property ModuleId() As Integer
            Get
                Return MyBase.ModuleId
            End Get
            Set(ByVal Value As Integer)
                'Need to make sure a ModuleConfiguration always exists
                If ModuleConfiguration Is Nothing Then
                    Dim modInfo As New ModuleInfo()
                    modInfo.TabModuleID = Null.NullInteger
                    ModuleConfiguration = modInfo
                End If

                ModuleConfiguration.ModuleID = Value
            End Set
        End Property

        Public Shadows Property TabModuleId() As Integer
            Get
                Return MyBase.TabModuleId
            End Get
            Set(ByVal Value As Integer)
                'Need to make sure a ModuleConfiguration always exists
                If ModuleConfiguration Is Nothing Then
                    Dim modInfo As New ModuleInfo()
                    modInfo.ModuleID = Null.NullInteger
                    ModuleConfiguration = modInfo
                End If

                ModuleConfiguration.TabModuleID = Value
            End Set
        End Property

        Public ReadOnly Property ModuleSettings() As Hashtable
            Get
                If _moduleSettings Is Nothing Then
                    'Get ModuleSettings
                    _moduleSettings = Portals.PortalSettings.GetModuleSettings(ModuleId)
                End If
                Return _moduleSettings
            End Get
        End Property

        Public ReadOnly Property TabModuleSettings() As Hashtable
            Get
                If _tabModuleSettings Is Nothing Then
                    'Get TabModuleSettings
                    _tabModuleSettings = Portals.PortalSettings.GetTabModuleSettings(TabModuleId)
                End If
                Return _tabModuleSettings
            End Get
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public Shadows ReadOnly Property Settings() As Hashtable
            Get
                If _settings Is Nothing Then
                    'Merge the TabModuleSettings and ModuleSettings
                    _settings = Portals.PortalSettings.GetTabModuleSettings(New Hashtable(ModuleSettings), New Hashtable(TabModuleSettings))
                End If
                Return _settings
            End Get
        End Property

#Region "Overridable Methods"

        Public Overridable Sub LoadSettings()
        End Sub

        Public Overridable Sub UpdateSettings()
        End Sub

#End Region

    End Class

End Namespace

