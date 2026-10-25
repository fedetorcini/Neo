using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

/// <summary>Repeatable integration checks, only in Play with an isolated save path.</summary>
public sealed class NeoRoomValidation
{
    [Serializable] public sealed class Result { public string scenario; public bool passed; }
    [Serializable] public sealed class Report { public List<Result> results = new List<Result>(); public string error; public bool finished; }
    public static Report Last;
    NeoFirstRoom room;
    DemoBootstrap flow;
    public static string Run()
    {
        if (!Application.isPlaying || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEO_DEMO_SAVE_PATH")))
            throw new InvalidOperationException("Se requiere Play y guardado de prueba aislado.");
        Last = new Report();
        var runner = new NeoRoomValidation();
        UnityEngine.Object.FindAnyObjectByType<NeoFirstRoom>().StartCoroutine(runner.Guarded());
        return "Validación iniciada";
    }
    IEnumerator Guarded()
    {
        var run = Scenarios();
        while (true)
        {
            object current;
            try { if (!run.MoveNext()) break; current = run.Current; }
            catch (Exception error) { Last.error = error.ToString(); break; }
            yield return current;
        }
        Last.finished = true;
        File.WriteAllText("Temp/neo-room-validation.json",JsonUtility.ToJson(Last,true));
    }
    void Check(string label, bool passed)
    {
        Last.results.Add(new Result { scenario=label, passed=passed });
        if (!passed) throw new InvalidOperationException(label);
    }
    static void Private(object target, string method) => target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
    IEnumerator Scenarios()
    {
        room=UnityEngine.Object.FindAnyObjectByType<NeoFirstRoom>(); flow=UnityEngine.Object.FindAnyObjectByType<DemoBootstrap>();
        if (DemoBootstrap.IsPaused) Private(flow,"Resume");
        if (!flow.TutorialComplete)
            typeof(NeoFirstRoom).GetMethod("GrantMachop",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(room,new object[]{true});
        room.ResetRoom();
        var human=room.human.GetComponent<NeoGridMotor>(); var partner=room.machop.GetComponent<NeoGridMotor>();
        Check("Machop real y un compañero registrado",room.controller.PartnerCount==1 && room.machop.GetComponentInChildren<Animator>().runtimeAnimatorController.name=="Machop");
        var start=human.Cell;
        Check("Paso diagonal solicitado se limita a cardinal",human.Step(Vector2.one));
        room.controller.SelectPartnerByIndex(0);
        Check("Cambio de control espera la llegada",room.controller.Active==room.human);
        yield return new WaitForSeconds(.35f);
        Check("Una casilla, sin diagonal",Mathf.Abs(human.Cell.x-start.x)+Mathf.Abs(human.Cell.y-start.y)==1);
        Check("Control transferido tras llegar",room.controller.Active==room.machop);
        human.Teleport(NeoGridBoard.Center(new Vector2Int(5,6)));
        partner.Teleport(NeoGridBoard.Center(new Vector2Int(15,8)));
        room.machop.RestoreEnergy();
        room.rock.Interact(room.human.GetComponent<Interactable>());
        Check("El humano no empuja",!room.rock.IsMoving);
        room.rock.Interact(room.machop.GetComponent<Interactable>());
        Check("Empuje reserva origen y destino",room.rock.IsMoving && room.board.Reserved(new Vector2Int(15,9)) && room.board.Reserved(new Vector2Int(15,10)));
        Check("Una reserva ajena bloquea el paso",!room.board.CanEnter(new Vector2Int(15,10),human));
        yield return new WaitForSeconds(.4f);
        Check("Empuje termina en una casilla",NeoGridBoard.Cell(room.rock.transform.position)==new Vector2Int(15,10));
        Check("Ocupación se actualiza",!room.board.Blocked(new Vector2Int(15,9)) && room.board.Blocked(new Vector2Int(15,10)));
        var source=room.board.Graph.GetNearest(NeoGridBoard.Center(new Vector2Int(15,9))).node;
        Check("A* libera el paso tras empujar",source.Walkable);
        partner.Teleport(NeoGridBoard.Center(new Vector2Int(15,9)));
        Check("Pared impide el paso cardinal",!partner.Step(Vector2.up));
        float energy=room.machop.Energy;
        flow.ShowRoomDialogue(new[]{"Prueba de pausa"},null);
        var position=partner.transform.position;
        yield return new WaitForSecondsRealtime(.4f);
        Check("Diálogo congela energía y movimiento",room.machop.Energy==energy && partner.transform.position==position);
        flow.StopAllCoroutines();
        Private(flow,"Resume");
        room.machop.RestoreEnergy();
        float deadline=Time.time+6;
        while(room.controller.Active==room.machop && Time.time<deadline) yield return null;
        Check("Agotamiento devuelve control al humano",room.controller.Active==room.human);
        room.controller.SelectPartnerByIndex(0);
        Check("Antes de 25% no permite retomar",room.controller.Active==room.human && room.machop.Energy<25);
        yield return new WaitForSeconds(2.7f);
        room.controller.SelectPartnerByIndex(0);
        Check("Desde 25% permite retomar",room.controller.Active==room.machop);
        room.controller.ReturnControlToHuman();
        human.Teleport(NeoGridBoard.Center(new Vector2Int(21,9)));
        partner.Teleport(NeoGridBoard.Center(new Vector2Int(14,9)));
        deadline=Time.time+5;
        while(Vector2.Distance(human.transform.position,partner.transform.position)>1.05f && Time.time<deadline) yield return null;
        Check("Seguimiento cruza el paso modificado",Vector2.Distance(human.transform.position,partner.transform.position)<=1.05f && partner.PathRequests>0);
        room.ResetRoom();
        Check("Reinicio restaura roca y libera reservas",NeoGridBoard.Cell(room.rock.transform.position)==room.rock.initialCell && !room.board.Reserved(room.rock.solutionCell));
        human.Teleport(NeoGridBoard.Center(new Vector2Int(15,8)));
        partner.Teleport(NeoGridBoard.Center(new Vector2Int(15,10)));
        room.controller.SelectPartnerByIndex(0);
        room.rock.Interact(room.machop.GetComponent<Interactable>());
        Check("Roca no entra en celda con humano",!room.rock.IsMoving);
        room.ResetRoom();
    }
}
