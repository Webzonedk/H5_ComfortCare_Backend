﻿using ComfortCare.Domain.BusinessLogic.interfaces;
using ComfortCare.Domain.Entities;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ComfortCare.Domain.BusinessLogic
{
    /// <summary>
    /// This class is used to generate routes to the ComfortCare Eco system, the generator
    /// will generate routes for a single day at a time, so wrap it in a foreach if you need to calculate
    /// multiple days
    /// </summary>
    public class RouteGenerator
    {
        #region fields
        private readonly IRouteRepo _routeRepo;
        #endregion

        #region Constructor
        public RouteGenerator(IRouteRepo routeRepo)
        {
            _routeRepo = routeRepo;
        }
        #endregion

        #region Public Methods

        public List<RouteEntity> CalculateDailyRoutes(int numberOfDays, int numberOfAssignments)
        {
            //return CalculateDailyRoutesInParrelel(numberOfDays, numberOfAssignments);
            //return CalculateDailyRoutesAsThreads(numberOfDays, numberOfAssignments);
            //return CalculateDailyRoutesAsTasks(numberOfDays, numberOfAssignments);
            return CalculateDailyRoutesAsTasksWithSemaphoreSlim(numberOfDays, numberOfAssignments);

        }


        public List<RouteEntity> CalculateDailyRoutesAsTasksWithSemaphoreSlim(int numberOfDays, int numberOfAssignments)
        {
#if DEBUG
            var stopwatch = new Stopwatch();
            stopwatch.Start();
#endif
            var routes = new List<RouteEntity>();
            var availableAssignments = _routeRepo.GetNumberOfAssignments(numberOfAssignments);
            var distances = _routeRepo.GetDistanceses(availableAssignments);

            // Create a list to hold the tasks
            List<Task> tasks = new List<Task>();
            object lockObject = new object(); // Used to synchronize access to 'routes'

            // Create a semaphore to limit the number of concurrent tasks
            SemaphoreSlim semaphore = new SemaphoreSlim(Environment.ProcessorCount); // Number of logical processors (minus 1 to ensure a bit of space if necessary to allow for other tasks)

            for (int dayIndex = 0; dayIndex < numberOfDays; dayIndex++)
            {
                int capturedDayIndex = dayIndex; // Capture the day index to avoid closure issues
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(); // Acquire the semaphore
                    try
                    {
                        var dailyRoutes = CalculateRoutesForSingleDay(capturedDayIndex, availableAssignments, distances);
                        lock (lockObject) // Synchronize access to 'routes'
                        {
                            routes.AddRange(dailyRoutes);
                        }
                    }
                    finally
                    {
                        semaphore.Release(); // Release the semaphore
                    }
                }));
            }

            // Wait for all tasks to complete
            Task.WhenAll(tasks).Wait();

            // Sort the routes
            routes = routes.OrderBy(o => o.RouteDate).ToList();

#if DEBUG
            stopwatch.Stop(); // Stop the stopwatch
            double elapsedMinutes = stopwatch.Elapsed.TotalMinutes;
            Console.WriteLine($"Total time used for the tasks: {elapsedMinutes}");
#endif

            
            return routes;
        }


        /// <summary>
        /// This method is used to calculate the routes for all the days by using tasks
        /// </summary>
        /// <param name="numberOfDays"></param>
        /// <param name="numberOfAssignments"></param>
        /// <returns></returns>
        public List<RouteEntity> CalculateDailyRoutesAsTasks(int numberOfDays, int numberOfAssignments)
        {
#if DEBUG
            var stopwatch = new Stopwatch();
            stopwatch.Start();
#endif
            var routes = new List<RouteEntity>();
            var availableAssignments = _routeRepo.GetNumberOfAssignments(numberOfAssignments);
            var distances = _routeRepo.GetDistanceses(availableAssignments);

            var tasks = new List<Task<List<RouteEntity>>>();
            //var currentDay = DateTime.Now.Date;
            List<Thread> threads = new List<Thread>();
            for (int dayIndex = 0; dayIndex < numberOfDays; dayIndex++)
            {
                int capturedDayIndex = dayIndex;
                tasks.Add(Task.Run(() => CalculateRoutesForSingleDay(capturedDayIndex, availableAssignments, distances)));
            }

            Task.WhenAll(tasks).Wait();

            foreach (var task in tasks)
            {
                routes.AddRange(task.Result);
            }
            routes = routes.OrderBy(o => o.RouteDate).ToList();
#if DEBUG
            stopwatch.Stop(); // Stop the stopwatch
            double elapsedMinutes = stopwatch.Elapsed.TotalMinutes;
            Console.WriteLine($"Total Time used for the tasks: {elapsedMinutes}");
#endif
            return routes;
        }




        public List<RouteEntity> CalculateDailyRoutesInParrelel(int numberOfDays, int numberOfAssignments) //Slower than threads
        {
#if DEBUG
            var stopwatch = new Stopwatch();
            stopwatch.Start();
#endif
            var availableAssignments = _routeRepo.GetNumberOfAssignments(numberOfAssignments);
            var distances = _routeRepo.GetDistanceses(availableAssignments);

            // Create a concurrent bag to hold the routes (thread safe)
            ConcurrentBag<RouteEntity> concurrentRoutes = new ConcurrentBag<RouteEntity>();
            // Create a list of threads
            Parallel.ForEach(Enumerable.Range(0, numberOfDays), dayIndex =>
            {
                var dailyRoutes = CalculateRoutesForSingleDay(dayIndex, availableAssignments, distances);
                foreach (var route in dailyRoutes)
                {
                    concurrentRoutes.Add(route);
                }
            });
            // Convert the concurrent bag to a list
            var routes = concurrentRoutes.ToList();
            // Sort the routes by date
            routes = routes.OrderBy(o => o.RouteDate).ToList();

#if DEBUG
            stopwatch.Stop(); // Stop the stopwatch
            double elapsedMinutes = stopwatch.Elapsed.TotalMinutes;
            Console.WriteLine($"Total time used for the threads: {elapsedMinutes}");
#endif

            return routes;
        }

        /// <summary>
        /// This method is used to calculate the routes for all the days by using threads
        /// </summary>
        /// <param name="numberOfDays"></param>
        /// <param name="numberOfAssignments"></param>
        /// <returns></returns>
        public List<RouteEntity> CalculateDailyRoutesAsThreads(int numberOfDays, int numberOfAssignments)
        {
#if DEBUG
            var stopwatch = new Stopwatch();
            stopwatch.Start();
#endif
            var routes = new List<RouteEntity>();
            var availableAssignments = _routeRepo.GetNumberOfAssignments(numberOfAssignments);
            var distances = _routeRepo.GetDistanceses(availableAssignments);

            // Create a list to hold the threads
            List<Thread> threads = new List<Thread>();
            object lockObject = new object(); // Used to synchronize access to 'routes'

            for (int dayIndex = 0; dayIndex < numberOfDays; dayIndex++)
            {
                int capturedDayIndex = dayIndex; // Capture the day index to avoid closure issues
                Thread thread = new Thread(() =>
                {
                    var dailyRoutes = CalculateRoutesForSingleDay(capturedDayIndex, availableAssignments, distances);
                    lock (lockObject) // Synchronize access to 'routes'
                    {
                        routes.AddRange(dailyRoutes);
                    }
                });
                thread.Start();
                threads.Add(thread);
            }

            // Wait for all threads to complete
            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            // Sort the routes
            routes = routes.OrderBy(o => o.RouteDate).ToList();

#if DEBUG
            stopwatch.Stop(); // Stop the stopwatch
            double elapsedMinutes = stopwatch.Elapsed.TotalMinutes;
            Console.WriteLine($"Total time used for the threads: {elapsedMinutes}");
#endif

            return routes;
        }



        /// <summary>
        /// This method is used to calculate the routes for a single day, and will be used by the thread method
        /// </summary>
        /// <param name="daysToAdd"></param>
        /// <param name="availableAssignments"></param>
        /// <param name="distances"></param>
        /// <returns>Returns a list of routes for a single day</returns>
        public List<RouteEntity> CalculateRoutesForSingleDay(int daysToAdd, List<AssignmentEntity> availableAssignments, List<DistanceEntity> distances)
        {
#if DEBUG           

            var stopwatch = new Stopwatch();
            stopwatch.Start();
#endif
            var currentDay = DateTime.Now.Date.AddDays(daysToAdd);
            //Creating a deep copy to avoid changing the original list
            var dailyAssignments = availableAssignments.Select(a => new AssignmentEntity
            {
                Id = a.Id,
                TimeWindowStart = a.TimeWindowStart,
                TimeWindowEnd = a.TimeWindowEnd,
                Duration = a.Duration,
                ArrivalTime = a.ArrivalTime
            }).ToList();

            var plannedRoutes = new List<RouteEntity>();

            foreach (var assignment in dailyAssignments)
            {
                NormalizeTimeWindows(currentDay, assignment);
            }

            while (dailyAssignments.Any())
            {
                var routeTimeTracker = currentDay;
                var startAssignment = dailyAssignments.OrderBy(o => o.TimeWindowStart).First();
                InitializeStartRouteAndAssignmentTimes(startAssignment, ref routeTimeTracker);

                var routeStartingTime = routeTimeTracker;
                var currentAssignment = startAssignment;
                var route = new List<AssignmentEntity> { startAssignment };

                while (currentAssignment != null)
                {
                    var nextAssignment = FindNextAssignment(currentAssignment, routeTimeTracker, dailyAssignments, distances, route);

                    if (nextAssignment != null)
                    {
                        var totalCurrentRouteHours = ((routeTimeTracker.AddSeconds(nextAssignment.Duration)) - routeStartingTime).TotalHours;

                        if (totalCurrentRouteHours < 8.8)
                        {
                            UpdateRouteTimeAndAssignment(nextAssignment, ref routeTimeTracker, route, distances);
                            currentAssignment = nextAssignment;
                        }
                        else
                        {
                            currentAssignment = null;
                        }
                    }
                    else
                    {
                        currentAssignment = null;
                    }
                    //Thread.Sleep(1);
                }

                AddPlannedRoute(plannedRoutes, route, currentDay);
                RemoveProcessedAssignments(dailyAssignments, route);
                //Thread.Sleep(10);
            }
#if DEBUG
            stopwatch.Stop(); // Stop the stopwatch
            double elapsedMinutes = stopwatch.Elapsed.TotalMinutes;
            Console.WriteLine($"Current day: {currentDay} - Method execution time: {elapsedMinutes}");
#endif
            return plannedRoutes;
        }





        #endregion

        #region Private Methods
        /// <summary>
        /// This method will set the date for the current
        /// assignments time window
        /// </summary>
        /// <param name="currentDay"></param>
        /// <param name="assignment"></param>
        private void NormalizeTimeWindows(DateTime currentDay, AssignmentEntity assignment)
        {
            assignment.TimeWindowStart = NormalizeTime(currentDay, assignment.TimeWindowStart);
            assignment.TimeWindowEnd = NormalizeTime(currentDay, assignment.TimeWindowEnd);

            if (assignment.TimeWindowEnd <= assignment.TimeWindowStart)
            {
                assignment.TimeWindowEnd = assignment.TimeWindowEnd.AddDays(1);
            }
        }

        /// <summary>
        /// Sets the current time frame
        /// </summary>
        /// <param name="currentDay"></param>
        /// <param name="time"></param>
        /// <returns>adjusted time frame</returns>
        private DateTime NormalizeTime(DateTime currentDay, DateTime time)
        {
            return currentDay.AddMilliseconds(time.TimeOfDay.TotalMilliseconds);
        }

        /// <summary>
        /// this method sets the time for the start of a new route and the time
        /// for the first assignment of the route
        /// </summary>
        /// <param name="assignment"></param>
        /// <param name="routeTimeTracker"></param>
        private void InitializeStartRouteAndAssignmentTimes(AssignmentEntity assignment, ref DateTime routeTimeTracker)
        {
            assignment.ArrivalTime = assignment.TimeWindowStart;
            routeTimeTracker = assignment.ArrivalTime.AddSeconds(assignment.Duration);
        }

        /// <summary>
        /// This method finds the next assignment that is in the current time frame, depended on the route time tracker
        /// and the distance from current to next assignment measured in time
        /// </summary>
        /// <param name="currentAssignment"></param>
        /// <param name="routeTimeTracker"></param>
        /// <param name="availableAssignments"></param>
        /// <param name="distances"></param>
        /// <param name="route"></param>
        /// <returns></returns>
        private AssignmentEntity FindNextAssignment(AssignmentEntity currentAssignment, DateTime routeTimeTracker,
                        List<AssignmentEntity> availableAssignments, List<DistanceEntity> distances, List<AssignmentEntity> route)
        {
            AssignmentEntity nextAssignment = null;
            double shortestDistanceToPotentialNext = double.MaxValue;

            var availableAssignmentsAtTheCurrentTime = availableAssignments.Where(a =>
                a != currentAssignment &&
                a.TimeWindowStart <= routeTimeTracker && a.TimeWindowEnd >= routeTimeTracker
                && !route.Any(routeAssignment => routeAssignment.Id == a.Id))
                .ToList();

            foreach (var potentialNextAssignment in availableAssignmentsAtTheCurrentTime)
            {
                var distanceToPotentialNext = distances.FirstOrDefault(d =>
                    (d.AssignmentOne == currentAssignment.Id && d.AssignmentTwo == potentialNextAssignment.Id) ||
                    (d.AssignmentTwo == currentAssignment.Id && d.AssignmentOne == potentialNextAssignment.Id));

                if (distanceToPotentialNext != null)
                {
                    var travelTimeFromCurrentToPotentialNext = distanceToPotentialNext.DistanceBetween;
                    if (travelTimeFromCurrentToPotentialNext != 0 && travelTimeFromCurrentToPotentialNext < shortestDistanceToPotentialNext)
                    {
                        shortestDistanceToPotentialNext = travelTimeFromCurrentToPotentialNext;
                        nextAssignment = potentialNextAssignment;
                    }
                }
            }

            return nextAssignment;
        }

        /// <summary>
        /// This method adds the next assignment to the current route list,
        /// and updates the time tracker for the route
        /// </summary>
        /// <param name="nextAssignment"></param>
        /// <param name="routeTimeTracker"></param>
        /// <param name="route"></param>
        /// <param name="distances"></param>
        private void UpdateRouteTimeAndAssignment(AssignmentEntity nextAssignment, ref DateTime routeTimeTracker, List<AssignmentEntity> route, List<DistanceEntity> distances)
        {
            routeTimeTracker = routeTimeTracker.AddSeconds(GetTravelTime(nextAssignment, distances));
            nextAssignment.ArrivalTime = routeTimeTracker;
            routeTimeTracker = routeTimeTracker.AddSeconds(nextAssignment.Duration);
            route.Add(nextAssignment);
        }

        /// <summary>
        /// Get the distance from current assignment to the next assignment
        /// </summary>
        /// <param name="assignment"></param>
        /// <param name="distances"></param>
        /// <returns>returns travel time in seconds</returns>
        private double GetTravelTime(AssignmentEntity assignment, List<DistanceEntity> distances)
        {
            var currentDistance = distances.FirstOrDefault(d =>
                (d.AssignmentOne == assignment.Id && d.AssignmentTwo == assignment.Id) ||
                (d.AssignmentTwo == assignment.Id && d.AssignmentOne == assignment.Id));

            return currentDistance?.DistanceBetween ?? 0;
        }

        /// <summary>
        /// This method adds the current route to the collection of planned routes
        /// </summary>
        /// <param name="plannedRoutes"></param>
        /// <param name="route"></param>
        /// <param name="currentDay"></param>
        private void AddPlannedRoute(List<RouteEntity> plannedRoutes, List<AssignmentEntity> route, DateTime currentDay)
        {
            plannedRoutes.Add(new RouteEntity() { RouteGuid = Guid.NewGuid(), Assignments = route, RouteDate = currentDay });
        }

        /// <summary>
        /// This method removes assignments add to the planned routes from the collection
        /// of assignments that are available
        /// </summary>
        /// <param name="availableAssignments"></param>
        /// <param name="route"></param>
        private void RemoveProcessedAssignments(List<AssignmentEntity> availableAssignments, List<AssignmentEntity> route)
        {
            availableAssignments.RemoveAll(route.Contains);
        }
        #endregion
    }
}
