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

Imports ICSharpCode.SharpZipLib.Zip
Imports System.IO
Imports System.Text
Imports DotNetNuke.Entities.Modules

Namespace DotNetNuke.Modules.Admin.ResourceInstaller

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The PaDnnInstaller_V2Skin extends PaDnnInstallerBase to support V2 Skin Objects
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	04/21/2005  documented
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class PaDnnInstaller_V2Skin
        Inherits PaDnnInstallerBase

#Region "Constructors"

        Public Sub New(ByVal InstallerInfo As PaInstallInfo)
            MyBase.New(InstallerInfo)
        End Sub

#End Region

#Region "Protected Methods"

        Protected Overrides Sub RegisterModules(ByVal Folder As PaFolder, ByVal Modules As ArrayList, ByVal Controls As ArrayList)

            InstallerInfo.Log.AddInfo(REGISTER_Controls)

            Dim objModuleControl As ModuleControlInfo
            For Each objModuleControl In Controls
                ' Skins Objects have a null ModuleDefID
                objModuleControl.ModuleDefID = Null.NullInteger

                ' check if control exists
                Dim objModuleControl2 As ModuleControlInfo = ModuleControlController.GetModuleControlByKeyAndSrc(Null.NullInteger, objModuleControl.ControlKey, objModuleControl.ControlSrc)
                If objModuleControl2 Is Nothing Then
                    ' add new control
                    ModuleControlController.AddModuleControl(objModuleControl)
                Else
                    ' update existing control 
                    objModuleControl.ModuleControlID = objModuleControl2.ModuleControlID
                    ModuleControlController.UpdateModuleControl(objModuleControl)
                End If
            Next

            InstallerInfo.Log.EndJob(REGISTER_End)

        End Sub

#End Region

    End Class

End Namespace
