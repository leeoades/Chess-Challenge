using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    private readonly Random _random = new();

    public Move Think(Board board, Timer timer)
    {
        // Things to check for
        // - hanging pieces
        // - pieces that are guarded fewer times that I am attacking
        // - pinning pieces ?
        
        // - moving my piece away from danger
        // - making a move that doesn't hang a piece of mine
        
        
        // Mate in 1 or Forced mate in 1,2,3?
        if (FindForcedMate(board, out var forcedMateMove)) return forcedMateMove; 

        // Hanging pieces?
        if (CaptureHangingPiece(board, out var captureHangingPieceMove)) return captureHangingPieceMove;

        // Capture Better Piece - win the exchange
        if (CanCaptureBetterPiece(board, out var captureBetterPiece))
        {
            Debug.WriteLine("I can capture a better piece here - " + captureBetterPiece);
            return captureBetterPiece;
        }
        
        // Move my piece away from danger
        if (RespondToThreatenedPieces(board, out var moveThreatenedPieceMove)) return moveThreatenedPieceMove;
        
        return GetRandomMove(board);
    }

    private bool CanCaptureBetterPiece(Board board, out Move finalMove)
    {
        finalMove = board.GetLegalMoves(true).Where(mv => mv.CapturePieceType > mv.MovePieceType).OrderBy(mv => mv.CapturePieceType).FirstOrDefault();
        return board.GetLegalMoves(true).Where(mv => mv.CapturePieceType > mv.MovePieceType).OrderBy(mv => mv.CapturePieceType).FirstOrDefault() != default;
    }

    private bool IsBadMove(Board board, Move move)
    {
        board.MakeMove(move);
        try
        {
            // Is stalemate?
            if (!board.GetLegalMoves().Any()) return true;
            
            return false;
        }
        finally
        {
            board.UndoMove(move);
        }
    }

    private bool RespondToThreatenedPieces(Board board, out Move finalMove)
    {
        var myThreatenedPieces = GetMyThreatenedPieces(board);

        foreach (var threatenedPiece in myThreatenedPieces)
        {
            Debug.WriteLine("My piece is being attacked: " + threatenedPiece);

            if (RespondToThreatOnMyPiece(board, threatenedPiece, out Move move))
            {
                finalMove = move;
                return true;
            };
        }

        finalMove = Move.NullMove;
        return false;
    }

    private IEnumerable<Piece> GetMyThreatenedPieces(Board board)
    {
        var myPieces = board.GetAllPieceLists().Where(pl => pl.IsWhitePieceList == board.IsWhiteToMove).OrderByDescending(pl => pl.TypeOfPieceInList).SelectMany(pl => pl).ToArray();
        var myThreatenedPieces = myPieces.Where(p => board.SquareIsAttackedByOpponent(p.Square));
        return myThreatenedPieces;
    }

    private bool IfPieceMovesToSquareIsItSafe(Board board, Piece myPiece, Move potentialMove)
    {
        board.MakeMove(potentialMove);
        try
        {
            return GetPiecesAttackingMe(board, myPiece.Square).Any();
        }
        finally
        {
            board.UndoMove(potentialMove);
        }
    }

    private IEnumerable<Move> GetPiecesAttackingMe(Board board, Square mySquare)
    {
        return board.GetLegalMoves(true)
            .Where(m => m.TargetSquare == mySquare)
            .OrderBy(m => m.MovePieceType);
    }

    private bool RespondToThreatOnMyPiece(Board board, Piece myPiece, out Move finalMove)
    {
        // Piece is under attack.
        // Responses:
        // - Is the piece under attack more valuable? e.g. ignore if pawn is being attacked. Always respond if queen being attacked
        var successfullySkippedTurn = board.TrySkipTurn();
        if (successfullySkippedTurn)
        {
            var whatPiecesAreAttackingMe = GetPiecesAttackingMe(board, myPiece.Square).ToArray();
            var myPieceIsInTrouble = whatPiecesAreAttackingMe.Any();
            board.UndoSkipTurn();
            if (myPieceIsInTrouble)
            {
                var escapeSquares = board.GetLegalMoves().Where(m => m.StartSquare == myPiece.Square);
                foreach (var potentialMove in escapeSquares)
                {
                    // Move to the first safe square
                    if (IfPieceMovesToSquareIsItSafe(board, myPiece, potentialMove))
                    {
                        // Skip bad moves
                        if (IsBadMove(board, potentialMove)) continue;
                        
                        Debug.WriteLine(myPiece + " is in trouble so moving it to " + potentialMove.TargetSquare);

                        finalMove = potentialMove;
                        return true;
                    }
                }
            }
        }
        

        // - Is the piece under attack hanging? e.g. if pawn under attack and is hanging, might still want to move it
        // - Can I capture a move valuable piece with it?
        // - Can I move it to a safe square?
        // - Capture any piece? (or do something else e.g. attack their piece)
        var myCaptures = board.GetLegalMoves(true).Where(c => c.StartSquare == myPiece.Square).ToArray();
        var pieceCanCaptureMoreValuablePiece = myCaptures.Where(pc => pc.CapturePieceType >= myPiece.PieceType).OrderByDescending(pc => pc.CapturePieceType);
        foreach (var potentialPieceCapture in pieceCanCaptureMoreValuablePiece)
        {
            finalMove = potentialPieceCapture;
            return true;
        }

        if (GetSafePlaceToEscape(board, myPiece, out Move safeMove))
        {
            finalMove = safeMove;
            return true;
        }
        
        var pieceCanCaptureLessValuablePiece = myCaptures.Where(pc => pc.CapturePieceType < myPiece.PieceType).OrderByDescending(pc => pc.CapturePieceType);
        foreach (var potentialPieceCapture in pieceCanCaptureLessValuablePiece)
        {
            finalMove = potentialPieceCapture;
            return true;
        }
    
        finalMove = Move.NullMove;
        return false;
    }

    private bool GetSafePlaceToEscape(Board board, Piece piece, out Move finalMove)
    {
        var myMoves = board.GetLegalMoves().Where(m => m.StartSquare == piece.Square).ToArray();
        var safeSquareMoves = myMoves
            .Where(m => !board.SquareIsAttackedByOpponent(m.TargetSquare))
            .Where(m => !IsBadMove(board, m))
            .ToArray();
        
        if (safeSquareMoves.Any())
        {
            finalMove = safeSquareMoves.First();
            return true;
        }
        finalMove = Move.NullMove;
        return false;
    }

    private bool CaptureHangingPiece(Board board, out Move finalMove)
    {
        var allCaptures = board.GetLegalMoves(true).OrderByDescending(mv => mv.CapturePieceType);
        foreach (var captureMove in allCaptures)
        {
            var squareOfCapture = captureMove.TargetSquare;
            try
            {
                board.MakeMove(captureMove);
                
                // Can the Opponent recapture?
                var opponentCaptures = board.GetLegalMoves(true);
                var opponentCanRecapture = opponentCaptures.Any(c => c.TargetSquare == squareOfCapture);
                if (!opponentCanRecapture)
                {
                    // Nope - so piece is hanging
                    Debug.WriteLine("I found a hanging piece to capture. Nom");
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

        finalMove = Move.NullMove;
        return false;
    }

    private Move GetRandomMove(Board board)
    {
        Debug.WriteLine("Resort to random move.");

        var legalMoves = board.GetLegalMoves();
        var firstGoodMove = legalMoves.FirstOrDefault(mv => !IsBadMove(board, mv));
        return 
            firstGoodMove != default 
                ? firstGoodMove 
                : legalMoves[0];
    }

    private bool FindForcedMate(Board board, out Move finalMove)
    {
        foreach (var move in board.GetLegalMoves())
        {
            if (DoesMoveLeadToForcesMate(board, move))
            {
                Debug.WriteLine("I found a forced checkmate.");
                finalMove = move;
                return true;
            }
        }
 
        finalMove = Move.NullMove;
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