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

Imports System.Reflection


Namespace DotNetNuke.Services.Scheduling


    Public Class ScheduleItem
        '''''''''''''''''''''''''''''''''''''''''''''''''''
        'This custom business object represents
        'a single item on the schedule.
        '''''''''''''''''''''''''''''''''''''''''''''''''''
        Private _ScheduleID As Integer
        Private _TypeFullName As String
        Private _TimeLapse As Integer
        Private _TimeLapseMeasurement As String
        Private _RetryTimeLapse As Integer
        Private _RetryTimeLapseMeasurement As String
        Private _ObjectDependencies As String
        Private _RetainHistoryNum As Integer
        Private _NextStart As Date
        Private _CatchUpEnabled As Boolean
        Private _Enabled As Boolean
        Private _AttachToEvent As String
        Private _ThreadID As Integer
        Private _ProcessGroup As Integer
        Private _ScheduleSource As ScheduleSource
        Private _ScheduleItemSettings As Hashtable
        Private _Servers As String

        Public Sub New()
            _ScheduleID = Null.NullInteger
            _TypeFullName = Null.NullString
            _TimeLapse = Null.NullInteger
            _TimeLapseMeasurement = Null.NullString
            _RetryTimeLapse = Null.NullInteger
            _RetryTimeLapseMeasurement = Null.NullString
            _ObjectDependencies = Null.NullString
            _RetainHistoryNum = Null.NullInteger
            _NextStart = Null.NullDate
            _CatchUpEnabled = Null.NullBoolean
            _Enabled = Null.NullBoolean
            _AttachToEvent = Null.NullString
            _ThreadID = Null.NullInteger
            _ProcessGroup = Null.NullInteger
            _Servers = Null.NullString
        End Sub



        Public Property ScheduleID() As Integer
            Get
                Return _ScheduleID
            End Get
            Set(ByVal Value As Integer)
                _ScheduleID = Value
            End Set
        End Property

        Public Property TypeFullName() As String
            Get
                Return _TypeFullName
            End Get
            Set(ByVal Value As String)
                _TypeFullName = Value
            End Set
        End Property

        Public Property TimeLapse() As Integer
            Get
                Return _TimeLapse
            End Get
            Set(ByVal Value As Integer)
                _TimeLapse = Value
            End Set
        End Property
        Public Property TimeLapseMeasurement() As String
            Get
                Return _TimeLapseMeasurement
            End Get
            Set(ByVal Value As String)
                _TimeLapseMeasurement = Value
            End Set
        End Property
        Public Property RetryTimeLapse() As Integer
            Get
                Return _RetryTimeLapse
            End Get
            Set(ByVal Value As Integer)
                _RetryTimeLapse = Value
            End Set
        End Property
        Public Property RetryTimeLapseMeasurement() As String
            Get
                Return _RetryTimeLapseMeasurement
            End Get
            Set(ByVal Value As String)
                _RetryTimeLapseMeasurement = Value
            End Set
        End Property
        Public Property ObjectDependencies() As String
            Get
                Return _ObjectDependencies
            End Get
            Set(ByVal Value As String)
                _ObjectDependencies = Value
            End Set
        End Property

        Public Property RetainHistoryNum() As Integer
            Get
                Return _RetainHistoryNum
            End Get
            Set(ByVal Value As Integer)
                _RetainHistoryNum = Value
            End Set
        End Property

        Public Property NextStart() As Date
            Get
                If _NextStart = Null.NullDate Then
                    _NextStart = Now
                End If
                Return _NextStart
            End Get
            Set(ByVal Value As Date)
                _NextStart = Value
            End Set
        End Property
        Public Property ScheduleSource() As ScheduleSource
            Get
                Return _ScheduleSource
            End Get
            Set(ByVal Value As ScheduleSource)
                _ScheduleSource = Value
            End Set
        End Property

        Public Property CatchUpEnabled() As Boolean
            Get
                Return _CatchUpEnabled
            End Get
            Set(ByVal Value As Boolean)
                _CatchUpEnabled = Value
            End Set
        End Property

        Public Property Enabled() As Boolean
            Get
                Return _Enabled
            End Get
            Set(ByVal Value As Boolean)
                _Enabled = Value
            End Set
        End Property

        Public Property AttachToEvent() As String
            Get
                Return _AttachToEvent
            End Get
            Set(ByVal Value As String)
                _AttachToEvent = Value
            End Set
        End Property

        Public Property ThreadID() As Integer
            Get
                Return _ThreadID
            End Get
            Set(ByVal Value As Integer)
                _ThreadID = Value
            End Set
        End Property

        Public Property ProcessGroup() As Integer
            Get
                Return _ProcessGroup
            End Get
            Set(ByVal Value As Integer)
                _ProcessGroup = Value
            End Set
        End Property

        Public Property Servers() As String
            Get
                Return _Servers
            End Get
            Set(ByVal Value As String)
                Value = Trim(Value)
                If Value.Length > 0 Then
                    If Value.IndexOf(",") > 0 Then
                        Value = "," + Value
                    End If
                    If Value.LastIndexOf(",") < Value.Length - 1 Then
                        Value += ","
                    End If
                End If
                _Servers = Value
            End Set
        End Property




        Public Function HasObjectDependencies(ByVal strObjectDependencies As String) As Boolean
            If strObjectDependencies.IndexOf(",") > -1 Then
                Dim a() As String
                a = Split(strObjectDependencies.ToLower, ",")
                Dim i As Integer
                For i = 0 To a.Length - 1
                    If a(i) = strObjectDependencies.ToLower Then
                        Return True
                    End If
                Next
            Else
                If ObjectDependencies.ToLower.IndexOf(strObjectDependencies.ToLower) > -1 Then
                    Return True
                End If
            End If

            Return False
        End Function

        Public Sub AddSetting(ByVal Key As String, ByVal Value As String)
            _ScheduleItemSettings.Add(Key, Value)
        End Sub
        Public Function GetSetting(ByVal Key As String) As String
            If _ScheduleItemSettings Is Nothing Then
                GetSettings()
            End If
            If _ScheduleItemSettings.ContainsKey(Key) Then
                Return Convert.ToString(_ScheduleItemSettings(Key))
            Else
                Return ""
            End If
        End Function
        Public Function GetSettings() As Hashtable
            _ScheduleItemSettings = SchedulingProvider.Instance.GetScheduleItemSettings(Me.ScheduleID)
            Return _ScheduleItemSettings
        End Function
    End Class

End Namespace
