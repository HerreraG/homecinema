﻿using AutoMapper;
using HomeCinema.Data.Infrastructure;
using HomeCinema.Data.Repositories;
using HomeCinema.Entities;
using HomeCinema.Web.Infrastructure.Core;
using HomeCinema.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace HomeCinema.Web.Controllers.Api {

    [Authorize(Roles = "Admin")]
    [RoutePrefix("api/rentals")]
    public class RentalsController : ApiControllerBase {

        private readonly IEntityBaseRepository<Rental> _rentalsRepository;
        private readonly IEntityBaseRepository<Customer> _customersRepository;
        private readonly IEntityBaseRepository<Stock> _stocksRepository;
        private readonly IEntityBaseRepository<Movie> _moviesRepository;

        public RentalsController(IEntityBaseRepository<Rental> rentalsRepository,
                                 IEntityBaseRepository<Customer> customersRepository,
                                 IEntityBaseRepository<Stock> stocksRepository,
                                 IEntityBaseRepository<Movie> moviesRepository,
                                 IEntityBaseRepository<Error> _errorsRepository,
                                 IUnitOfWork _unitOfWork)
                                : base(_errorsRepository, _unitOfWork) {

            this._rentalsRepository = rentalsRepository;
            this._customersRepository = customersRepository;
            this._stocksRepository = stocksRepository;
            this._moviesRepository = moviesRepository;
        }

        [HttpGet]
        [Route("{id:int}/rentalhistory")]
        public HttpResponseMessage RentalHistory(HttpRequestMessage request, int id) {

            return CreateHttpResponse(request, () => {
                HttpResponseMessage response = null;
                List<RentalHistoryViewModel> _rentalHistory = this.GetMovieRentalHistory(id);

                response = request.CreateResponse<List<RentalHistoryViewModel>>(HttpStatusCode.OK, _rentalHistory);

                return response;
            });
        }

        [HttpPost]
        [Route("rent/{customerId:int}/{stockId:int}")]
        public HttpResponseMessage Rent(HttpRequestMessage request, int customerId, int stockId) {
            return CreateHttpResponse(request, () => {
                HttpResponseMessage response = null;

                var customer = this._customersRepository.GetSingle(customerId);
                var stock = this._stocksRepository.GetSingle(stockId);

                if(customer == null || stock == null) {
                    response = request.CreateErrorResponse(HttpStatusCode.NotFound, "Invalid customer or stock");
                } else {
                    if(stock.IsAvailable) { 
                        Rental _rental = new Rental() {
                            CustomerId = customerId,
                            StockId = stockId,
                            RentalDate = DateTime.Now,
                            Status = "Borrowed"
                        };

                        this._rentalsRepository.Add(_rental);
                        stock.IsAvailable = false;

                        _unitOfWork.Commit();

                        RentalViewModel rentalVm = Mapper.Map<Rental, RentalViewModel>(_rental);

                        response = request.CreateResponse<RentalViewModel>(HttpStatusCode.OK, rentalVm);
                    } else {
                        response = request.CreateErrorResponse(HttpStatusCode.BadRequest, "Selected stock is not available anymore");
                    }
                }
                return response;
            })
        }

        [HttpPost]
        [Route("return/{rentalId:int}")]
        public HttpResponseMessage Return(HttpRequestMessage request, int rentalId) {

            return CreateHttpResponse(request, () => {
                HttpResponseMessage response = null;

                var rental = this._rentalsRepository.GetSingle(rentalId);

                if (rental == null) {
                    response = request.CreateResponse(HttpStatusCode.NotFound, 'Invalid Rental');
                } else {
                    rental.Status = "Returned";
                    rental.Stock.IsAvailable = true;
                    rental.RentalDate = DateTime.Now;

                    _unitOfWork.Commit();

                    response = request.CreateResponse(HttpStatusCode.OK);
                }

                return response;
            });
        }

        private List<RentalHistoryViewModel> GetMovieRentalHistory(int movieId) {

            List<RentalHistoryViewModel> _rentalHistory = new List<RentalHistoryViewModel>();
            List<Rental> rentals = new List<Rental>();

            var movies = this._moviesRepository.GetSingle(movieId);

            foreach(var stock in movies.Stocks ) {
                rentals.AddRange(stock.Rentals);
            }

            foreach(var rental in rentals) {
                RentalHistoryViewModel _historyItem = new RentalHistoryViewModel() {
                    Id = rental.Id,
                    StockId = rental.StockId,
                    RentalDate = rental.RentalDate,
                    ReturnedDate = rental.ReturnedDate.HasValue ? rental.ReturnedDate : null,
                    Status = rental.Status,
                    Customer = this._customersRepository.GetCustomerFullName(rental.CustomerId)
                };

                _rentalHistory.Add(_historyItem);
            }

            _rentalHistory.Sort((r1, r2) => r2.RentalDate.CompareTo(r1.RentalDate));

            return _rentalHistory;
        }

    }
}