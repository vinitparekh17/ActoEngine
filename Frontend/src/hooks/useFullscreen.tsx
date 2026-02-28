import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { useLocation } from 'react-router-dom';

interface FullscreenContextType {
    isFullscreen: boolean;
    toggleFullscreen: () => void;
    exitFullscreen: () => void;
}

const FullscreenContext = createContext<FullscreenContextType | undefined>(undefined);

export const FullscreenProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const [isFullscreen, setIsFullscreen] = useState(false);
    const location = useLocation();

    const toggleFullscreen = useCallback(() => {
        setIsFullscreen((prev) => !prev);
    }, []);

    const exitFullscreen = useCallback(() => {
        setIsFullscreen(false);
    }, []);

    // Handle Escape key to exit fullscreen
    useEffect(() => {
        if (!isFullscreen) return;

        const handleKeyDown = (e: KeyboardEvent) => {
            if (e.key === 'Escape') {
                exitFullscreen();
            }
        };

        window.addEventListener('keydown', handleKeyDown);
        return () => window.removeEventListener('keydown', handleKeyDown);
    }, [isFullscreen, exitFullscreen]);

    useEffect(() => {
        exitFullscreen();
    }, [location.pathname, exitFullscreen]);

    return (
        <FullscreenContext.Provider value={{ isFullscreen, toggleFullscreen, exitFullscreen }}>
            {children}
        </FullscreenContext.Provider>
    );
};

export const useFullscreen = () => {
    const context = useContext(FullscreenContext);
    if (context === undefined) {
        throw new Error('useFullscreen must be used within a FullscreenProvider');
    }
    return context;
};
