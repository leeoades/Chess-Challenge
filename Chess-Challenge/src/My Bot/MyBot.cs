using System;
using System.Linq;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    private Random _random = new();

    public Move Think(Board board, Timer timer)
    {
        // Things to check for
        // - hanging pieces
        // - pieces that are guarded fewer times that I am attacking
        // - pinning pieces
        // - 
        
        // Mate in 1 or Forced mate in 1,2,3?
        if (FindForcedMate(board, out var forcedMateMove)) return forcedMateMove; 

        // Hanging pieces?
        if (CaptureHangingPiece(board, out var captureHangingPieceMove)) return captureHangingPieceMove;        
        

        return GetRandomMove(board);
    }

    private bool CaptureHangingPiece(Board board, out Move finalMove)
    {
        finalMove = Move.NullMove;
        
        var allCaptures = board.GetLegalMoves(true);
        foreach (var captureMove in allCaptures)
        {
            var squareOfCapture = captureMove.TargetSquare;
            try
            {
                board.MakeMove(captureMove);
                
                // Opponent captures
                var opponentCaptures = board.GetLegalMoves(true);
                var opponentCanRecapture = opponentCaptures.Any(c => c.TargetSquare == squareOfCapture);
                if (!opponentCanRecapture)
                {
                    finalMove = captureMove;
                    return true;
                }
                
                // TODO: Consider if opponent can only capture lesser pieces
            }
            finally
            {
                board.UndoMove(captureMove);
            }
        }

        return false;
    }

    private Move GetRandomMove(Board board)
    {
        var legalMoves = board.GetLegalMoves();
        return legalMoves[_random.Next(legalMoves.Length)];
    }

    private bool FindForcedMate(Board board, out Move finalMove)
    {
        finalMove = Move.NullMove;

        foreach (var move in board.GetLegalMoves())
        {
            if (DoesMoveLeadToForcesMate(board, move))
            {
                finalMove = move;
                return true;
            }
        }

        return false;
    }

    private bool DoesMoveLeadToForcesMate(Board board, Move move, int remainingDepth = 3)
    {
        // If making a move reduces my opponent to only a small number of moves then explore whether there is a forced mate.
        // ie opponent only has 2 moves and both lead to mate
        const int numberOfOpponentResponses = 3;

        board.MakeMove(move);
        try
        {
            if (board.IsInCheckmate()) return true;
            if (remainingDepth == 0) return false;
            
            var legalMoves = board.GetLegalMoves();
            return 
                legalMoves.Length <= numberOfOpponentResponses 
                && legalMoves.All(m => DoesMoveLeadToForcesMate(board, m, remainingDepth-1));
        }
        finally
        {
            board.UndoMove(move);
        }
    }


    // Test if this move gives checkmate
    private bool MoveIsCheckmate(Board board, Move move)
    {
        board.MakeMove(move);
        bool isMate = board.IsInCheckmate();
        board.UndoMove(move);
        return isMate;
    }
}