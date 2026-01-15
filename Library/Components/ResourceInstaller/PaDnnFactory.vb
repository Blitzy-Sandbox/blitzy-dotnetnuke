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

Imports System.IO
Imports System.Xml
Imports DotNetNuke.Entities.Modules.Definitions

Namespace DotNetNuke.Modules.Admin.ResourceInstaller
    Public Class PaDnnLoaderFactory
        Inherits ResourceInstallerBase

        Private _installerInfo As PaInstallInfo

        Public Sub New(ByVal InstallerInfo As PaInstallInfo)
            _installerInfo = InstallerInfo
        End Sub

        Public ReadOnly Property InstallerInfo() As PaInstallInfo
            Get
                Return _installerInfo
            End Get
        End Property

        Public Function GetDnnAdapter() As PaDnnAdapterBase

            Dim Version As ModuleDefinitionVersion = GetModuleVersion()
            Dim retValue As PaDnnAdapterBase = Nothing

            Select Case Version
                Case ModuleDefinitionVersion.V2
                    retValue = New PaDnnAdapter_V2(InstallerInfo)
                Case ModuleDefinitionVersion.V3
                    retValue = New PaDnnAdapter_V3(InstallerInfo)
                Case ModuleDefinitionVersion.V2_Skin
                    retValue = New PaDnnAdapter_V2Skin(InstallerInfo)
                Case ModuleDefinitionVersion.V2_Provider
                    retValue = New PaDnnAdapter_V2Provider(InstallerInfo)
                Case ModuleDefinitionVersion.VUnknown
                    Throw New Exception(EXCEPTION_Format)
            End Select

            Return retValue
        End Function

        Public Function GetDnnInstaller() As PaDnnInstallerBase
            Dim Version As ModuleDefinitionVersion = GetModuleVersion()
            Dim retValue As PaDnnInstallerBase = Nothing

            Select Case Version
                Case ModuleDefinitionVersion.V2
                    retValue = New PaDnnInstallerBase(InstallerInfo)
                Case ModuleDefinitionVersion.V3
                    retValue = New PaDnnInstaller_V3(InstallerInfo)
                Case ModuleDefinitionVersion.V2_Skin
                    retValue = New PaDnnInstaller_V2Skin(InstallerInfo)
                Case ModuleDefinitionVersion.V2_Provider
                    retValue = New PaDnnInstaller_V2Provider(InstallerInfo)
                Case ModuleDefinitionVersion.VUnknown
                    Throw New Exception(EXCEPTION_Format)
            End Select

            Return retValue
        End Function
        Private Function GetModuleVersion() As ModuleDefinitionVersion
            If Not InstallerInfo.DnnFile Is Nothing Then
                Dim buffer As New MemoryStream(InstallerInfo.DnnFile.Buffer, False)
                Dim xval As New ModuleDefinitionValidator
                Return xval.GetModuleDefinitionVersion(buffer)
            Else
                Return ModuleDefinitionVersion.VUnknown
            End If
        End Function
    End Class
End Namespace
