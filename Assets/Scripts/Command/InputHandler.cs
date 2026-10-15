using System.Collections.Generic;
using UnityEngine;

public class InputHandler : MonoBehaviour
{
    public PlayerMovement playerMovement;
    public PlayerShooting playerShooting;

    //Queue untuk menyimpan list command
    Queue<Command> commands = new Queue<Command>();

    void FixedUpdate()
    {
        // Disabled movement also blocks commands after player death.
        if (playerMovement == null || !playerMovement.isActiveAndEnabled)
        {
            return;
        }

        Command moveCommand = InputMovementHandling();
        if (moveCommand != null)
        {
            commands.Enqueue(moveCommand);

            moveCommand.Execute();
        }
    }

    void Update()
    {
        //Mengahndle shoot
        Command shootCommand = InputShootHandling();
        if (shootCommand != null)
        {
            shootCommand.Execute();
        }
    }

    Command InputMovementHandling()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        if (h != 0f || v != 0f)
        {
            return new MoveCommand(playerMovement, h, v);
        }

        if (Input.GetKey(KeyCode.Z))
        {
            return Undo();
        }

        // Keep the idle command so the walking animation stops.
        return new MoveCommand(playerMovement, 0f, 0f);
    }

    Command Undo()
    {
        //Jika Queue command tidak kosong, lakukan perintah undo
        if (commands.Count > 0)
        {
            Command undoCommand = commands.Dequeue();
            undoCommand.UnExecute();
        }
        return null;
    }

    Command InputShootHandling()
    {
        if (playerShooting == null || !playerShooting.isActiveAndEnabled)
        {
            return null;
        }

        if (Time.timeScale != 0f && Input.GetButton("Fire1"))
        {
            return new ShootCommand(playerShooting);
        }

        return null;
    }
}