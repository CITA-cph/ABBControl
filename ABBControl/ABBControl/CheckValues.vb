Imports Grasshopper.Kernel
Imports Grasshopper.Kernel.Data
Imports Grasshopper.Kernel.Types
Imports RapidD = ABB.Robotics.Controllers.RapidDomain

Public Class CheckValues
    Inherits GH_Component

    Public Sub New()
        MyBase.New("Check Variables", "Variables", "Check variables and expire if they reach a certain state", "CITA", "Robots")
        SetupTimer()
    End Sub

    Public Overrides ReadOnly Property ComponentGuid As Guid
        Get
            Return New Guid("{766BFABE-E384-4024-8BD9-0B58AC04F625}")
        End Get
    End Property

    Protected Overrides Sub RegisterInputParams(pManager As GH_InputParamManager)
        pManager.AddTextParameter("Task", "T", "Task name", GH_ParamAccess.tree)
        pManager.AddTextParameter("Module", "M", "Module name", GH_ParamAccess.tree)

        pManager.AddTextParameter("Variables", "V", "Variables to trigger the component expiration", GH_ParamAccess.tree)
        pManager.AddTextParameter("Values", "Q", "Variable value to expire on", GH_ParamAccess.tree)

        pManager.AddIntegerParameter("Interval", "I", "Time interval (in ms) between each check", GH_ParamAccess.tree, 1000)
    End Sub

    Protected Overrides Sub RegisterOutputParams(pManager As GH_OutputParamManager)
        pManager.AddTextParameter("Value", "V", "Variables", GH_ParamAccess.list)
    End Sub

    Public Overrides Sub RemovedFromDocument(document As GH_Document)
        MyBase.RemovedFromDocument(document)
        RemoveTimer()
    End Sub

    Dim interval As Integer = -1
    Dim tim As System.Timers.Timer = Nothing
    Dim expact As New System.Action(AddressOf ExpireThisComponent)

    Dim globtask As New String("")
    Dim globmod As New String("")
    Dim variables As New SortedList(Of String, String)
    Dim expiration As New SortedList(Of String, String)

    Protected Overrides Sub SolveInstance(DA As IGH_DataAccess)
        If Not ConnectionComponent.ConnectionOpened Then
            Me.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Open the connection first")
            Return
        End If

        Dim alltasks As New GH_Structure(Of GH_String)
        Dim allmods As New GH_Structure(Of GH_String)
        Dim allvars As New GH_Structure(Of GH_String)
        Dim allvals As New GH_Structure(Of GH_String)
        Dim allints As New GH_Structure(Of GH_Integer)

        If Not DA.GetDataTree(0, alltasks) Then Return
        If Not DA.GetDataTree(1, allmods) Then Return
        If Not DA.GetDataTree(2, allvars) Then Return
        If Not DA.GetDataTree(3, allvals) Then Return
        If Not DA.GetDataTree(4, allints) Then Return

        Dim listtasks As New List(Of String)
        Dim listmods As New List(Of String)
        Dim listvars As New List(Of String)
        Dim listvals As New List(Of String)
        Dim listints As New List(Of Integer)

        For Each ghs As GH_String In alltasks.AllData(True)
            listtasks.Add(New String(ghs.Value))
        Next

        For Each ghs As GH_String In allmods.AllData(True)
            listmods.Add(New String(ghs.Value))
        Next

        For Each ghs As GH_String In allvars.AllData(True)
            listvars.Add(New String(ghs.Value))
        Next

        For Each ghs As GH_String In allvals.AllData(True)
            listvals.Add(New String(ghs.Value))
        Next

        For Each ghs As GH_Integer In allints.AllData(True)
            listints.Add(ghs.Value)
        Next

        If listvars.Count <> listvals.Count Then
            Me.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Variables.Count <> Values.Count")
            Return
        End If

        SyncLock globtask
            globtask = listtasks(0)
        End SyncLock

        SyncLock globmod
            globmod = listmods(0)
        End SyncLock

        SyncLock variables
            SyncLock expiration

                Dim oldvariables As New SortedList(Of String, String)(variables)
                Dim oldexpiration As New SortedList(Of String, String)(expiration)

                variables.Clear()
                oldexpiration.Clear()

                For i As Integer = 0 To listvars.Count - 1 Step 1
                    Dim thisvar As String = listvars(i)
                    Dim thisval As String = listvals(i)

                    If oldvariables.ContainsKey(thisvar) Then
                        variables(thisvar) = oldvariables(thisvar)
                        expiration(thisvar) = thisval
                    Else
                        variables.Add(thisvar, thisval)
                        expiration.Add(thisvar, thisval)
                    End If
                Next

            End SyncLock
        End SyncLock

        System.Threading.Interlocked.Exchange(interval, listints(0))

    End Sub

    Public Sub SetupTimer()
        If interval = -1 Then Return
        RemoveTimer()
        tim = New System.Timers.Timer(interval)
        AddHandler tim.Elapsed, AddressOf OnTick
        tim.Start()
    End Sub

    Public Sub RemoveTimer()
        If tim IsNot Nothing Then
            tim.Stop()
            RemoveHandler tim.Elapsed, AddressOf OnTick
            tim.Dispose()
            tim = Nothing
        End If
    End Sub

    Public Sub OnTick(sender As Object, e As System.Timers.ElapsedEventArgs)
        If CheckValues() Then
            If Rhino.RhinoApp.MainApplicationWindow.InvokeRequired Then Rhino.RhinoApp.MainApplicationWindow.Invoke(expact)
        End If
    End Sub

    Public Function CheckValues() As Boolean 'returns true if there is a need to expire 
        Dim doExpire As Boolean = False
        If Not ConnectionComponent.ConnectionOpened Then Return False

        SyncLock ConnectionComponent.ABBController

            'create local values and get a copy of the global values
            Dim taskname As New String("")
            Dim modname As New String("")

            SyncLock globtask
                taskname = New String(globtask)
            End SyncLock

            SyncLock globmod
                modname = New String(globmod)
            End SyncLock

            'query the shared controller 
            Dim thisTask As RapidD.Task = ConnectionComponent.ABBController.Rapid.GetTask(taskname)
            Dim thisMod As RapidD.Module = thisTask.GetModule(modname)
            Dim thisProps As New RapidD.RapidSymbolSearchProperties(RapidD.SymbolSearchMethod.Block, RapidD.SymbolTypes.Variable, True, False, True, False)
            Dim thisSearch() As RapidD.RapidSymbol = thisMod.SearchRapidSymbol(thisProps)

            'search the dictionaries
            SyncLock variables
                SyncLock expiration
                    For Each result As RapidD.RapidSymbol In thisSearch
                        Dim thisData As New RapidD.RapidData(ConnectionComponent.ABBController, result)

                        Dim thisName As String = thisData.Name
                        Dim thisVal As String = thisData.Value.ToString

                        If variables.ContainsKey(thisName) Then
                            Dim oldvalue As String = variables(thisName)
                            Dim newvalue As String = thisVal
                            Dim expirationCase As String = expiration(thisName)

                            Select Case expirationCase
                                Case "ignore"
                                Case "onchange"
                                    If oldvalue <> newvalue Then doExpire = True
                                Case Else
                                    If oldvalue = newvalue Then doExpire = True
                            End Select

                            variables(thisName) = newvalue
                        End If

                    Next
                End SyncLock

            End SyncLock

        End SyncLock

        Return doExpire
    End Function

    Public Sub ExpireThisComponent()
        Me.ExpireSolution(True)
    End Sub

End Class
